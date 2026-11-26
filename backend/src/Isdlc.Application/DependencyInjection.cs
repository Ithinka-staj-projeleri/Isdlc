using Isdlc.Application.Agents;
using Isdlc.Application.Assessment;
using Isdlc.Application.Dashboard;
using Isdlc.Application.Audit;
using Isdlc.Application.Documents;
using Isdlc.Application.Identity;
using Isdlc.Application.Meetings;
using Isdlc.Application.Notifications;
using Isdlc.Application.OpenIssues;
using Isdlc.Application.Process;
using Isdlc.Application.Projects;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.ProcessImprovements;
using Isdlc.Application.Requirements;
using Isdlc.Application.Risks;
using Isdlc.Application.Search;
using Isdlc.Application.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Application services. Each module adds its own service here.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProcessService, ProcessService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountStatusService, AccountStatusService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IOpenIssueService, OpenIssueService>();
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ITimelineService, TimelineService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectSummaryService, ProjectSummaryService>();
        services.AddScoped<ITeamWorkloadService, TeamWorkloadService>();
        services.AddScoped<IRequirementService, RequirementService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IRiskService, RiskService>();
        services.AddScoped<IAssessmentService, AssessmentService>();
        services.AddScoped<IQuestionBankService, QuestionBankService>();
        services.AddScoped<IQuestionnaireService, QuestionnaireService>();
        services.AddScoped<IRequestAnalysisService, RequestAnalysisService>();
        services.AddScoped<IProcessImprovementService, ProcessImprovementService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<ITenantSettingsService, TenantSettingsService>();
        services.AddScoped<ICustomFieldService, CustomFieldService>();
        services.AddScoped<IAiUsageService, AiUsageService>();

        // Modules send through INotificationService; the inbox reads and marks the signed-in user's own notifications.
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationInboxService, NotificationInboxService>();

        return services;
    }
}
