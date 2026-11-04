using Isdlc.Application.Process;
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

        return services;
    }
}
