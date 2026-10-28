using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ParkFlow.Infrastructure.Realtime;
using ParkFlow.Application;
using ParkFlow.Application.Common;
using ParkFlow.Infrastructure;
using ParkFlow.Persistence;
using System.Text;
using System.Linq;
using ParkFlow.API.Security;
using Microsoft.AspNetCore.HttpOverrides;

var checkDatabaseOnly = args.Contains("--check-database", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--check-database").ToArray());
ReleaseConfiguration.Validate(builder.Configuration, builder.Environment);
builder.Services.Configure<ForwardedHeadersOptions>(options => ReleaseConfiguration.ConfigureProxy(options, builder.Configuration));

if (string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_URLS"]))
{
    builder.WebHost.UseUrls(builder.Environment.IsDevelopment() ? "http://0.0.0.0:5000" : "http://127.0.0.1:5000");
}

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? Array.Empty<string>();

// Add services to the container.

// Dependency Injections:
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (builder.Environment.IsDevelopment() && allowedOrigins.Length == 0) policy.SetIsOriginAllowed(_ => true);
        else policy.WithOrigins(allowedOrigins);
        policy.AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<ParkFlow.API.Security.PrivateDocumentLinks>();

// Swagger configuration:
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName);
});

// JWT Authentication configuration:
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is missing"));
builder.Services.AddAuthentication(option =>
{
    option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{   
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];

            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(ParkFlow.API.Security.AuthorizationPolicies.Configure);

builder.Services.AddSignalR();

var app = builder.Build();

// Release preflight: inspect schema history without seeding, serving requests or migrating.
if (checkDatabaseOnly)
{
    await using var scope = app.Services.CreateAsyncScope();
    await DatabaseStartupCheck.EnsureReadyAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    app.Logger.LogInformation("Database migration history matches this API release. No changes were applied.");
    return;
}

// Configure the HTTP request pipeline.
app.UseReleaseTransport(app.Environment.IsDevelopment());
app.UseSwagger();
app.UseSwaggerUI();

// Middleware configuration:
// app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

// A concurrent registration may hit the unique index after its initial ID check.
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (!context.Response.HasStarted &&
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: "IX_Vehicles_NormalizedPlate" })
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(Result<object>.Failure("This plate number is already registered.", ErrorCode.Conflict));
    }
    catch (ParkingCapacityException ex) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(Result<object>.Failure(ex.Message, ErrorCode.Conflict));
    }
    catch (RegistrationIdConflictException ex) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(Result<object>.Failure(ex.Message, ErrorCode.Conflict));
    }
});

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<NotificationHub>("/hubs/notifications");

// Seed default SuperAdmin account
await ParkFlow.Persistence.SuperAdminSeeder.SeedSuperAdminAsync(app.Services);
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<ParkFlow.Application.Interfaces.ISystemSettingsRepository>().LoadAsync();

app.Logger.LogInformation("==================================================");
app.Logger.LogInformation("🚀 ParkFlow API server started and listening!");
app.Logger.LogInformation("📖 Swagger Documentation: http://localhost:5000/swagger");
app.Logger.LogInformation("==================================================");

app.Run();
