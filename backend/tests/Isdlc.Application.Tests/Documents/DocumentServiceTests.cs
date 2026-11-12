using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Documents;
using Isdlc.Application.Documents.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Audit;
using Isdlc.Domain.Documents;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Documents;

/// <summary>
/// Document tracking on a real database with the seeded document list. Every test that saves creates its own
/// project (TestProjects), so seed projects are never changed. Expectations: docs/requirements-documents-api.md
/// </summary>
[Collection("Database")]
public class DocumentServiceTests(DatabaseFixture database)
{
    private async Task<T> AsAsync<T>(string? email, Func<IDocumentService, Task<T>> action)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var service = new DocumentService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId));
        return await action(service);
    }

    private async Task<int> TypeIdAsync(string code)
    {
        await using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().DocumentTypes
            .Where(t => t.Code == code).Select(t => t.Id).SingleAsync();
    }

    private Task<ProjectDocumentDto> SaveAsync(
        int projectId, int documentTypeId, DocumentStatus status, string? note = null, string? link = null) =>
        AsAsync(TestProjects.Analyst, s => s.UpdateAsync(
            projectId, documentTypeId, new UpdateProjectDocumentDto { Status = status, Note = note, Link = link },
            CancellationToken.None));

    // ---- Document Library ----

    [Fact]
    public async Task ListTypes_Returns18DocumentsInStageOrder()
    {
        var types = await AsAsync(TestProjects.Sales, s => s.ListTypesAsync(CancellationToken.None));

        Assert.Equal(18, types.Count);
        Assert.Equal("MIB", types[0].Code);
        Assert.Equal("PTT", types[^1].Code);
        Assert.Equal(types.Select(t => t.StageOrder).Order(), types.Select(t => t.StageOrder));
        Assert.All(types, t => Assert.False(string.IsNullOrWhiteSpace(t.StageShortTitle)));
    }

    // ---- List ----

    [Fact]
    public async Task List_NewProject_EveryDocumentNotStarted()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");

        var documents = await AsAsync(TestProjects.Developer, s => s.ListAsync(projectId, CancellationToken.None));

        Assert.Equal(18, documents.Count);
        Assert.All(documents, d =>
        {
            Assert.Equal(DocumentStatus.NotStarted, d.Status);
            Assert.Null(d.UpdatedBy);
            Assert.Null(d.UpdatedAt);
        });
    }

    [Fact]
    public async Task List_ProjectMissing_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            AsAsync(TestProjects.Analyst, s => s.ListAsync(int.MaxValue, CancellationToken.None)));
    }

    // ---- Update ----

    [Fact]
    public async Task Update_FirstTime_CreatesRecordShownInList()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var typeId = await TypeIdAsync("PKD");

        var saved = await SaveAsync(projectId, typeId, DocumentStatus.InReview, "Müşteri yorumu bekleniyor.",
            "https://sirket.sharepoint.com/pkd");
        var documents = await AsAsync(TestProjects.Analyst, s => s.ListAsync(projectId, CancellationToken.None));

        Assert.Equal("PKD", saved.DocumentType.Code);
        Assert.Equal(DocumentStatus.InReview, saved.Status);
        Assert.NotNull(saved.UpdatedBy);
        Assert.NotNull(saved.UpdatedAt);
        var row = documents.Single(d => d.DocumentType.Id == typeId);
        Assert.Equal(DocumentStatus.InReview, row.Status);
        Assert.Equal("Müşteri yorumu bekleniyor.", row.Note);
        Assert.Equal("https://sirket.sharepoint.com/pkd", row.Link);
        Assert.All(documents.Where(d => d.DocumentType.Id != typeId), d => Assert.Equal(DocumentStatus.NotStarted, d.Status));
    }

    [Fact]
    public async Task Update_SecondTime_UpdatesTheSameRecord()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var typeId = await TypeIdAsync("PGD");

        await SaveAsync(projectId, typeId, DocumentStatus.InProgress, "Taslak");
        var saved = await SaveAsync(projectId, typeId, DocumentStatus.Approved);

        Assert.Equal(DocumentStatus.Approved, saved.Status);
        Assert.Null(saved.Note);
        await using var scope = database.CreateScope();
        var count = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().ProjectDocuments
            .CountAsync(d => d.ProjectId == projectId && d.DocumentTypeId == typeId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Update_BlankNoteAndLink_AreStoredAsEmpty()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");

        var saved = await SaveAsync(projectId, await TypeIdAsync("KTR"), DocumentStatus.NotRequired, "   ", " ");

        Assert.Equal(DocumentStatus.NotRequired, saved.Status);
        Assert.Null(saved.Note);
        Assert.Null(saved.Link);
    }

    [Fact]
    public async Task Update_ProjectNotActive_ThrowsConflict()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Rejected);
        var typeId = await TypeIdAsync("PKD");

        await Assert.ThrowsAsync<ConflictException>(() => SaveAsync(projectId, typeId, DocumentStatus.Approved));
    }

    [Fact]
    public async Task Update_UnknownDocumentType_ThrowsNotFound()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");

        await Assert.ThrowsAsync<NotFoundException>(() => SaveAsync(projectId, int.MaxValue, DocumentStatus.Approved));
    }

    [Fact]
    public async Task Update_NotSignedIn_ThrowsForbidden()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var typeId = await TypeIdAsync("PKD");

        await Assert.ThrowsAsync<ForbiddenException>(() => AsAsync(null, s => s.UpdateAsync(
            projectId, typeId, new UpdateProjectDocumentDto { Status = DocumentStatus.Approved }, CancellationToken.None)));
    }

    [Fact]
    public async Task Update_WritesAuditLogWithProject()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");

        await SaveAsync(projectId, await TypeIdAsync("TPL"), DocumentStatus.InProgress);

        await using var scope = database.CreateScope();
        var log = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().AuditLogs
            .AsNoTracking()
            .SingleAsync(l => l.EntityName == nameof(ProjectDocument) && l.ProjectId == projectId);
        Assert.Equal(AuditAction.Created, log.Action);
    }
}
