using Microsoft.Extensions.DependencyInjection;
using ParkFlow.Application.Interfaces;
using ParkFlow.Infrastructure.QrCode;
using ParkFlow.Infrastructure.Realtime;
using ParkFlow.Infrastructure.Security;

namespace ParkFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher, PasswordHasherService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IQrCodeService, QrCodeService>();
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<ISignalRNotificationSender, SignalRNotificationSender>();
        return services;
    }
}
