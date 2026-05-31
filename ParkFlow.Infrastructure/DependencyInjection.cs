using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkFlow.Application.Interfaces;
using ParkFlow.Infrastructure.Cloudinary;
using ParkFlow.Infrastructure.Email;
using ParkFlow.Infrastructure.QrCode;
using ParkFlow.Infrastructure.Realtime;
using ParkFlow.Infrastructure.Security;
using Resend;

namespace ParkFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IPasswordHasher, PasswordHasherService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IQrCodeService, QrCodeService>();
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();

        // SignalR
        services.AddScoped<ISignalRNotificationSender, SignalRNotificationSender>();

        // Cloudinary
        services.Configure<CloudinarySettings>(
            configuration.GetSection("CloudinarySettings"));
        services.AddSingleton<ICloudinaryService, CloudinaryService>();

         // Resend
        services.Configure<ResendClientOptions>(
            configuration.GetSection("Resend"));

        services.AddHttpClient<ResendClient>();
        services.AddTransient<IResend, ResendClient>();

        services.AddScoped<IEmailService, ResendEmailService>();
        
        return services;
    }
}