using Isdlc.Application.Common.Interfaces;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Common;

/// <summary>
/// Creates projects for tests that change data, so seed projects stay as they are (AGENTS.md → Bölüm 9).
/// Seed users: the e-mails in Isdlc.Infrastructure/Seed/users.json.
/// </summary>
public static class TestProjects
{
    public const string Analyst = "elif.yilmaz@isdlc.local";        // BusinessAnalyst
    public const string Manager = "mert.kaya@isdlc.local";          // SoftwareDepartmentManager
    public const string ProjectManager = "ayse.demir@isdlc.local";  // ProjectManager
    public const string Developer = "can.ozturk@isdlc.local";       // SoftwareDeveloper
    public const string Sales = "selin.koc@isdlc.local";            // SalesRepresentative

    /// <summary>
    /// A new project. Active and waiting at the given step, or closed with the given status (no current step).
    /// The analyst is Elif Yılmaz and the project manager Ayşe Demir.
    /// </summary>
    public static async Task<int> CreateAsync(
        DatabaseFixture database, string? stepCode, ProjectStatus status = ProjectStatus.Active)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var project = new Project
        {
            // Not an ISDLC code, so the year numbering of real requests is not affected.
            Code = $"T-{Guid.NewGuid():N}"[..14],
            Name = $"Test projesi ({stepCode ?? status.ToString()})",
            Description = "Test için oluşturuldu.",
            Source = RequestSource.Internal,
            RequestChannel = RequestChannel.Written,
            RequestType = RequestType.NewProject,
            CustomerName = "Test",
            RequesterName = "Test",
            BusinessAnalystId = await UserIdAsync(database, Analyst),
            ProjectManagerId = await UserIdAsync(database, ProjectManager),
            Status = status,
            CurrentStepId = stepCode is null ? null : await StepIdAsync(database, stepCode),
            StartDate = new DateOnly(2026, 11, 1),
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    public static async Task<Project> GetAsync(DatabaseFixture database, int projectId)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await db.Projects.AsNoTracking().Include(p => p.CurrentStep).SingleAsync(p => p.Id == projectId);
    }

    public static async Task<int> StepIdAsync(DatabaseFixture database, string stepCode)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await db.ProcessSteps.Where(s => s.Code == stepCode).Select(s => s.Id).SingleAsync();
    }

    public static async Task<int> UserIdAsync(DatabaseFixture database, string email)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
    }

    /// <summary>A seed project by its code (read-only use).</summary>
    public static async Task<int> SeedProjectIdAsync(DatabaseFixture database, string code)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await db.Projects.Where(p => p.Code == code).Select(p => p.Id).SingleAsync();
    }
}
