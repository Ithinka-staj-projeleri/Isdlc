using Isdlc.Application.Agents;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Infrastructure.Agents;
using Isdlc.Infrastructure.Identity;
using Isdlc.Application.Identity;
using Isdlc.Infrastructure.Persistence;
using Isdlc.Infrastructure.Persistence.Interceptors;
using Isdlc.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Isdlc.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<AuditLogInterceptor>();
        services.AddDbContext<IsdlcDbContext>((sp, options) => options
            // Queries that load several collections run as one SQL query (EF Core's default, set explicitly).
            // For a large collection, a query can opt into AsSplitQuery().
            .UseSqlServer(connectionString, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery))
            // Order matters: audit fields are filled first, then the log reads them.
            .AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<AuditLogInterceptor>()));

        // Application services see the same DbContext instance through this interface.
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<IsdlcDbContext>());

        services.AddScoped<DatabaseSeeder>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<UserPasswordSeeder>();

        AddAgents(services, configuration);

        return services;
    }

    /// <summary>The AI service client (ai/). Without Agents:ServiceUrl the AI features are off and the screens hide them.</summary>
    private static void AddAgents(IServiceCollection services, IConfiguration configuration)
    {
        var options = new AgentOptions
        {
            ServiceUrl = configuration["Agents:ServiceUrl"],
            ServiceKey = configuration["Agents:ServiceKey"],
        };

        // One HttpClient for the app's lifetime; connections are renewed so a changed address (DNS) is picked up.
        var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
        {
            // A model call with one retry can take a while.
            Timeout = TimeSpan.FromSeconds(90),
        };
        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            http.BaseAddress = new Uri(options.ServiceUrl.TrimEnd('/') + "/");
        }

        services.AddSingleton<IMeetingAgentClient>(sp =>
            new HttpMeetingAgentClient(
                http, options, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<HttpMeetingAgentClient>>()));
    }
}
