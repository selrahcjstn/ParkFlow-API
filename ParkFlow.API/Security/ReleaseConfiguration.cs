using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace ParkFlow.API.Security;

public static class ReleaseConfiguration
{
    public static Uri? Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment()) return null;
        var raw = configuration["Release:PublicApiUrl"]?.Trim();
        if (raw == null || raw.Contains('\\') || raw.Any(char.IsControl) || raw.IndexOfAny(['?', '#']) >= 0 || !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(raw, UriKind.Absolute, out var url) || url.Scheme != "https" ||
            string.IsNullOrEmpty(url.Host) || url.UserInfo.Length != 0 || url.Query.Length != 0 || url.Fragment.Length != 0 ||
            url.AbsolutePath.TrimEnd('/') != "/api")
            throw new InvalidOperationException("Release:PublicApiUrl must be a configured HTTPS endpoint ending in /api without credentials, query or fragment. No HTTP fallback is allowed.");
        if (configuration.GetValue<bool>("Seeding:IncludeTestData"))
            throw new InvalidOperationException("Seeding:IncludeTestData is allowed only in Development. Production sample provisioning is blocked.");
        foreach (var origin in configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var cors) || cors.Scheme != "https" || cors.UserInfo.Length != 0 ||
                cors.Query.Length != 0 || cors.Fragment.Length != 0 || cors.AbsolutePath != "/")
                throw new InvalidOperationException("Cors:AllowedOrigins must contain HTTPS origins without paths or credentials outside Development.");
        return url;
    }

    public static void ConfigureProxy(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Framework loopback defaults remain; never trust arbitrary forwarded headers.
        foreach (var value in configuration.GetSection("Release:KnownProxies").Get<string[]>() ?? [])
        {
            if (!IPAddress.TryParse(value, out var address))
                throw new InvalidOperationException("Release:KnownProxies must contain explicit proxy IP addresses.");
            options.KnownProxies.Add(address);
        }
    }

    public static IApplicationBuilder UseReleaseTransport(this IApplicationBuilder app, bool development)
    {
        app.UseForwardedHeaders();
        if (!development) app.Use(async (context, next) =>
        {
            if (!context.Request.IsHttps)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { message = "HTTPS is required." });
                return;
            }
            await next(context);
        });
        return app;
    }
}
