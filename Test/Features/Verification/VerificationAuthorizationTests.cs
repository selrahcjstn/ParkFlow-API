using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ParkFlow.API.Controllers;
using ParkFlow.API.Security;
using ParkFlow.Infrastructure.Security;
using Xunit;

namespace Test.Features.Verification;

public class VerificationAuthorizationTests
{
    [Fact]
    public async Task PasswordTokenStillAuthorizesAfterProductionJwtClaimMapping()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-only-key-that-is-at-least-thirty-two-bytes",
            ["Jwt:Issuer"] = "parkflow-test", ["Jwt:Audience"] = "parkflow-api", ["Jwt:ExpiryMinutes"] = "10"
        }).Build();
        var token = new JwtService(config).GenerateToken(new UserAccount("hash", null), "superadmin", "pwd");
        var user = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = config["Jwt:Issuer"], ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"], ValidateLifetime = true, ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!))
        }, out _);
        var services = new ServiceCollection(); services.AddLogging(); services.AddAuthorization(AuthorizationPolicies.Configure);
        using var provider = services.BuildServiceProvider();
        Assert.True((await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, "SuperAdminOnly")).Succeeded);
    }

    [Theory]
    [InlineData("Admin", "AdminOrSuperAdmin", true)]
    [InlineData("SuperAdmin", "AdminOrSuperAdmin", true)]
    [InlineData("Student", "AdminOrSuperAdmin", false)]
    [InlineData("Admin", "SuperAdminOnly", false)]
    [InlineData("SuperAdmin", "SuperAdminOnly", true)]
    public async Task ProductionPoliciesEnforceRoles(string role, string policy, bool expected)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddAuthorization(AuthorizationPolicies.Configure);
        using var provider = services.BuildServiceProvider();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role), new Claim("parkflow_authentication_method", "pwd")], "test"));
        Assert.Equal(expected, (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, policy)).Succeeded);
    }

    [Fact]
    public async Task AdministrativeRoleWithoutVerifiedPasswordCannotReachApprovalPolicies()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddAuthorization(AuthorizationPolicies.Configure);
        using var provider = services.BuildServiceProvider();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "SuperAdmin"), new Claim("parkflow_authentication_method", "unverified")], "test"));
        var auth = provider.GetRequiredService<IAuthorizationService>();
        Assert.False((await auth.AuthorizeAsync(user, null, "SuperAdminOnly")).Succeeded);
        Assert.False((await auth.AuthorizeAsync(user, null, "AdminOrSuperAdmin")).Succeeded);
    }

    [Fact]
    public async Task UnauthenticatedClaimCannotModifyPolicies()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddAuthorization(AuthorizationPolicies.Configure);
        using var provider = services.BuildServiceProvider();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "SuperAdmin")]));
        Assert.False((await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(anonymous, null, "SuperAdminOnly")).Succeeded);
    }

    [Theory]
    [InlineData(nameof(VerificationController.UpdatePolicies), "SuperAdminOnly")]
    [InlineData(nameof(VerificationController.Policies), "AdminOrSuperAdmin")]
    [InlineData(nameof(VerificationController.Records), "AdminOrSuperAdmin")]
    [InlineData(nameof(VerificationController.Audit), "AdminOrSuperAdmin")]
    [InlineData(nameof(VerificationController.Review), "AdminOrSuperAdmin")]
    [InlineData(nameof(VerificationController.ReviewSubject), "AdminOrSuperAdmin")]
    public void VerificationApiHasRequiredAuthorization(string method, string policy) =>
        Assert.Contains(typeof(VerificationController).GetMethod(method)!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(), x => x.Policy == policy);

    [Fact]
    public void ExistingApprovalEndpointsRequireAdmins()
    {
        foreach (var method in new[] { typeof(CorSubmissionController).GetMethod("Validate")!, typeof(VehicleController).GetMethod("ValidateVehicle")! })
            Assert.Contains(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(), x => x.Policy == "AdminOrSuperAdmin");
    }
}
