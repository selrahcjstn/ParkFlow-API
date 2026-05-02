using Microsoft.Extensions.DependencyInjection;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher, PasswordHasherService>();

        return services;
    }
}
