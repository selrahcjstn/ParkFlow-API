using Microsoft.AspNetCore.Authorization;

namespace ParkFlow.API.Security;

public static class AuthorizationPolicies
{
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy("StudentOnly", p => p.RequireClaim("profile_type", "student"));
        options.AddPolicy("PersonnelOnly", p => p.RequireClaim("profile_type", "personnel"));
        options.AddPolicy("SuperAdminOnly", p => p.RequireAuthenticatedUser().RequireClaim("parkflow_authentication_method", "pwd").RequireAssertion(ctx =>
            ctx.User.IsInRole("SuperAdmin") || ctx.User.HasClaim("role", "SuperAdmin") || ctx.User.HasClaim("profile_type", "superadmin")));
        options.AddPolicy("AdminOrSuperAdmin", p => p.RequireAuthenticatedUser().RequireClaim("parkflow_authentication_method", "pwd").RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") || ctx.User.IsInRole("SuperAdmin") || ctx.User.HasClaim("role", "Admin") ||
            ctx.User.HasClaim("role", "SuperAdmin") || ctx.User.HasClaim("profile_type", "admin") || ctx.User.HasClaim("profile_type", "superadmin")));
    }
}
