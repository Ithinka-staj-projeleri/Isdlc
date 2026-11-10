using Isdlc.Application.Agents;
using Isdlc.Application.Audit;
using Isdlc.Application.Documents;
using Isdlc.Application.Identity;
using Isdlc.Application.Meetings;
using Isdlc.Application.Notifications;
using Isdlc.Application.OpenIssues;
using Isdlc.Application.Process;
using Isdlc.Application.Projects;
using Isdlc.Application.Requirements;
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
        services.AddScoped<IUserService, UserService>();

        // TEMPORARY until the Notifications module: replace with the real service.
        services.AddScoped<INotificationService, NullNotificationService>();

        return services;
    }
}
