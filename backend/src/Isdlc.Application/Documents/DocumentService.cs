using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Documents.Dtos;
using Isdlc.Application.OpenIssues;
using Isdlc.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Documents;

public class DocumentService(IApplicationDbContext dbContext, ICurrentUserService currentUser) : IDocumentService
{
    public async Task<IReadOnlyList<ProjectDocumentDto>> ListAsync(int projectId, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken);

        var types = await TypeSummaries().ToListAsync(cancellationToken);
        var saved = await SavedDocuments(projectId).ToDictionaryAsync(x => x.DocumentTypeId, cancellationToken);

        // A document never saved on the project counts as NotStarted.
        return types
            .Select(type => saved.TryGetValue(type.Id, out var doc)
                ? new ProjectDocumentDto(type, doc.Status, doc.Note, doc.Link, doc.UpdatedBy, doc.UpdatedAt)
                : new ProjectDocumentDto(type, DocumentStatus.NotStarted, null, null, null, null))
            .ToList();
    }

    public async Task<ProjectDocumentDto> UpdateAsync(
        int projectId, int documentTypeId, UpdateProjectDocumentDto request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        }

        await dbContext.GetWritableProjectAsync(projectId, cancellationToken);

        var type = await TypeSummaries(documentTypeId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Doküman bulunamadı.");

        var document = await dbContext.ProjectDocuments.SingleOrDefaultAsync(
            x => x.ProjectId == projectId && x.DocumentTypeId == documentTypeId, cancellationToken);
        if (document is null)
        {
            document = new ProjectDocument { ProjectId = projectId, DocumentTypeId = documentTypeId };
            dbContext.ProjectDocuments.Add(document);
        }

        // [Required] on the DTO guarantees a status.
        document.Status = request.Status!.Value;
        document.Note = OpenIssueRules.Clean(request.Note);
        document.Link = OpenIssueRules.Clean(request.Link);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (document.Id == 0)
        {
            // Two people saved the same document for the first time at the same moment; the unique index stopped one.
            throw new ConflictException("Doküman aynı anda başka biri tarafından kaydedildi. Lütfen tekrar deneyin.");
        }

        var doc = await SavedDocuments(projectId, documentTypeId).SingleAsync(cancellationToken);
        return new ProjectDocumentDto(type, doc.Status, doc.Note, doc.Link, doc.UpdatedBy, doc.UpdatedAt);
    }

    public async Task<IReadOnlyList<DocumentTypeDto>> ListTypesAsync(CancellationToken cancellationToken) =>
        await dbContext.DocumentTypes
            .AsNoTracking()
            .OrderBy(t => t.ProcessStage.Order)
            .ThenBy(t => t.Order)
            .Select(t => new DocumentTypeDto(
                t.Id, t.Code, t.Name, t.ProcessStage.Order, t.ProcessStage.ShortTitle, t.Description))
            .ToListAsync(cancellationToken);

    // ---- Helpers ----

    // Filters go before Select: EF cannot translate a filter on the projected DTO.

    /// <summary>All document types in stage order, or only the given one.</summary>
    private IQueryable<DocumentTypeSummaryDto> TypeSummaries(int? documentTypeId = null) =>
        dbContext.DocumentTypes
            .AsNoTracking()
            .Where(t => documentTypeId == null || t.Id == documentTypeId)
            .OrderBy(t => t.ProcessStage.Order)
            .ThenBy(t => t.Order)
            .Select(t => new DocumentTypeSummaryDto(t.Id, t.Code, t.Name, t.ProcessStage.Order, t.ProcessStage.ShortTitle));

    /// <summary>The project's saved documents (or only the given one) with who saved them last.</summary>
    private IQueryable<SavedDocument> SavedDocuments(int projectId, int? documentTypeId = null) =>
        dbContext.ProjectDocuments
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && (documentTypeId == null || x.DocumentTypeId == documentTypeId))
            .Select(x => new SavedDocument(
                x.DocumentTypeId,
                x.Status,
                x.Note,
                x.Link,
                dbContext.Users
                    .Where(u => u.Id == (x.UpdatedById ?? x.CreatedById))
                    .Select(u => new UserSummaryDto(u.Id, u.FullName))
                    .First(),
                x.UpdatedAt ?? x.CreatedAt));

    private sealed record SavedDocument(
        int DocumentTypeId, DocumentStatus Status, string? Note, string? Link, UserSummaryDto UpdatedBy, DateTime UpdatedAt);
}
