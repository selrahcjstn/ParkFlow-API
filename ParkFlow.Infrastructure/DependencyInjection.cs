using Microsoft.Extensions.DependencyInjection;
using ParkFlow.Application.Interfaces;
using ParkFlow.Infrastructure.Security;

namespace ParkFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher, PasswordHasherService>();
        services.AddScoped<IJwtService, JwtService>();
        return services;
    }
}
