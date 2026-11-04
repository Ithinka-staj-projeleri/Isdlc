using Isdlc.Application.Common.Interfaces;
using Isdlc.Infrastructure.Identity;
using Isdlc.Infrastructure.Persistence;
using Isdlc.Infrastructure.Persistence.Interceptors;
using Isdlc.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddSingleton(TimeProvider.System);

        // TEMPORARY current user until the Identity module adds sign-in.
        services.AddSingleton<TemporaryCurrentUserService>();
        services.AddSingleton<ICurrentUserService>(sp => sp.GetRequiredService<TemporaryCurrentUserService>());

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

        return services;
    }
}
