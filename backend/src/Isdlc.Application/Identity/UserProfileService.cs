using System.Globalization;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Dashboard;
using Isdlc.Application.Dashboard.Dtos;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Identity;

/// <summary>
/// The profile reads the dashboard's own lists (DashboardService) and the team workload, so a person's numbers
/// are the same on the dashboard, on their profile and in the owner selector. Contract: docs/users-api.md
/// </summary>
public class UserProfileService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    ITeamWorkloadService teamWorkload) : IUserProfileService
{
    private static readonly StringComparer TurkishNames = StringComparer.Create(new CultureInfo("tr-TR"), ignoreCase: true);

    public async Task<UserProfileDto> GetAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id, u.FullName, u.Email, u.IsActive, u.Expertise, u.WorkingNote,
                Permissions = u.Permissions.OrderBy(p => p.Permission).Select(p => p.Permission).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);

        // The same filter as the workload and "Bana atanan açık konular": owned, not closed, on an active project.
        var openIssues = await DashboardService.OpenIssueListAsync(
            dbContext.OpenIssues.Where(x => x.OwnerUserId == user.Id && x.Status != OpenIssueStatus.Closed
                && x.Project.Status == ProjectStatus.Active),
            today, cancellationToken);

        UserWorkloadCountsDto? workload = null;
        var waitingSteps = new DashboardListDto<WaitingStepDto>(0, []);
        if (user.IsActive)
        {
            var row = (await teamWorkload.GetAsync(cancellationToken)).Single(w => w.User.Id == user.Id);
            workload = new UserWorkloadCountsDto(row.OpenIssues, row.OverdueOpenIssues, row.WaitingSteps);
            waitingSteps = await DashboardService.WaitingStepsAsync(dbContext, user.Id, user.Permissions, now, cancellationToken);
        }

        return new UserProfileDto(
            user.Id, user.FullName, user.Email, user.Permissions, user.IsActive, user.Expertise, user.WorkingNote,
            workload, openIssues, waitingSteps);
    }

    public async Task<UserProfileDto> UpdateMineAsync(UpdateMyProfileDto dto, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");

        user.Expertise = NullIfEmpty(dto.Expertise);
        user.WorkingNote = NullIfEmpty(dto.WorkingNote);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(user.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<UserWorkloadDto>> ListWorkloadAsync(CancellationToken cancellationToken)
    {
        var expertise = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new { u.Id, u.Expertise })
            .ToDictionaryAsync(u => u.Id, u => u.Expertise, cancellationToken);

        var workload = await teamWorkload.GetAsync(cancellationToken);

        return workload
            .Select(w => new UserWorkloadDto(
                w.User.Id, w.User.FullName, w.Permissions, expertise.GetValueOrDefault(w.User.Id),
                w.OpenIssues, w.OverdueOpenIssues, w.WaitingSteps))
            .OrderBy(w => w.FullName, TurkishNames)
            .ToList();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
