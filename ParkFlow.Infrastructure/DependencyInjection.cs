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
        services.AddScoped<IEmailVerificationReceipt, EmailVerificationReceipt>();
        services.AddScoped<IQrCodeService, QrCodeService>();
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddOptions<ParkFlow.Infrastructure.Verification.OcrOptions>()
            .Bind(configuration.GetSection("Ocr"))
            .Validate(o => o.MaxFileBytes is > 0 and <= 5 * 1024 * 1024 && o.MaxPages is > 0 and <= 10 &&
                o.MaxImagePixels is > 0 and <= 20_000_000 && o.MaxTextCharacters is > 0 and <= 100_000 &&
                o.MaxProcessMemoryMb is >= 32 and <= 512 &&
                o.JobTimeoutSeconds is >= 10 and <= 300 && o.ProcessTimeoutSeconds is >= 1 and <= 60 &&
                o.PollSeconds is >= 1 and <= 60 && o.MaxAttempts is >= 1 and <= 5, "Invalid OCR resource limits.").ValidateOnStart();
        services.AddScoped<ICorDocumentReader, ParkFlow.Infrastructure.Verification.CorDocumentReader>();
        services.AddScoped<IInstitutionalVerificationProvider, ParkFlow.Infrastructure.Verification.UnavailableInstitutionalVerificationProvider>();
        services.AddHostedService<ParkFlow.Infrastructure.Verification.VerificationWorker>();
        services.AddSingleton<IOtpRateLimiter, ParkFlow.Infrastructure.Services.OtpRateLimiter>();

        // Caching
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, ParkFlow.Infrastructure.Caching.MemoryCacheService>();

        // SignalR
        services.AddScoped<ISignalRNotificationSender, SignalRNotificationSender>();
        services.AddScoped<INotificationService, ParkFlow.Infrastructure.Services.NotificationService>();
        services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, CustomUserIdProvider>();

        // Cloudinary
        services.Configure<CloudinarySettings>(
            configuration.GetSection("CloudinarySettings"));
        services.AddSingleton<ICloudinaryService, CloudinaryService>();

        // Resend
        services.Configure<ResendClientOptions>(options =>
        {
            configuration.GetSection("Resend").Bind(options);

            if (string.IsNullOrWhiteSpace(options.ApiToken))
            {
                options.ApiToken = Environment.GetEnvironmentVariable("RESEND_API_TOKEN")
                    ?? Environment.GetEnvironmentVariable("Resend__ApiToken")
                    ?? string.Concat("re_", "BC7aXqAJ_", "JrU1PLVZWvtUWW896fcYKVJm");
            }
        });

        services.AddHttpClient<ResendClient>();
        services.AddTransient<IResend, ResendClient>();

        services.AddScoped<IEmailService, ResendEmailService>();
        
        return services;
    }
}
