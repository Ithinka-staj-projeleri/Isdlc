using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Common;

/// <summary>The project fields a project record (open issue, meeting, ...) needs.</summary>
public record ProjectRef(int Id, string Code);

/// <summary>
/// Shared checks for project records (docs/data-model.md → "Proje kayıtları"):
/// they can be read on any project but written only on an active one.
/// </summary>
public static class ProjectAccess
{
    /// <summary>404 when the project does not exist.</summary>
    public static async Task<ProjectRef> GetProjectAsync(
        this IApplicationDbContext dbContext, int projectId, CancellationToken cancellationToken) =>
        await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new ProjectRef(p.Id, p.Code))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Proje bulunamadı.");

    /// <summary>404 when the project does not exist, 409 when it is not active.</summary>
    public static async Task<ProjectRef> GetWritableProjectAsync(
        this IApplicationDbContext dbContext, int projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.Code, p.Status })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Proje bulunamadı.");

        if (project.Status != ProjectStatus.Active)
        {
            throw new ConflictException("Proje aktif değil; kayıt eklenemez veya değiştirilemez.");
        }

        return new ProjectRef(project.Id, project.Code);
    }
}
