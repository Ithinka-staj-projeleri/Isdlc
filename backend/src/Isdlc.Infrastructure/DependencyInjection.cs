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
        services.AddDbContext<IsdlcDbContext>((sp, options) => options
            .UseSqlServer(connectionString)
            .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
