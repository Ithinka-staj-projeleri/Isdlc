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
using Isdlc.Application.Requirements;
using Isdlc.Application.Risks;
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
        services.AddScoped<IOpenIssueService, OpenIssueService>();
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IRequirementService, RequirementService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRiskService, RiskService>();
        services.AddScoped<IAssessmentService, AssessmentService>();
        services.AddScoped<IQuestionBankService, QuestionBankService>();
        services.AddScoped<IQuestionnaireService, QuestionnaireService>();

        // Modules send through INotificationService; the inbox reads and marks the signed-in user's own notifications.
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationInboxService, NotificationInboxService>();

        return services;
    }
}
