using Microsoft.Extensions.DependencyInjection;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System;
using System.Threading.Tasks;

namespace ParkFlow.Persistence;

public static class SuperAdminSeeder
{
    public static async Task SeedSuperAdminAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userAccountRepository = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        var authIdentityRepository = scope.ServiceProvider.GetRequiredService<IAuthIdentityRepository>();
        var userProfileRepository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var adminRepository = scope.ServiceProvider.GetRequiredService<IAdminRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var superAdminEmail = "superadmin@parkflow.com";
        var existingUser = await userAccountRepository.GetByEmailAsync(superAdminEmail);

        if (existingUser == null)
        {
            var hashedPassword = passwordHasher.HashPassword("SuperAdmin123!");
            var user = new UserAccount(hashedPassword, "+639000000000");
            user.UpdateOnboardingStep(OnboardingStep.Done);
            user.Verify();
            await userAccountRepository.AddAsync(user);

            var identity = AuthIdentity.CreateManual(user.Id, superAdminEmail, hashedPassword, isPrimary: true);
            identity.MarkVerified();
            await authIdentityRepository.AddAsync(identity);

            var userProfile = new UserProfile(
                user.Id,
                "Super",
                "Admin",
                "System",
                null);

            await userProfileRepository.AddAsync(userProfile);

            var admin = new Admin(userProfile, RoleLevel.SuperAdmin);
            await adminRepository.AddAsync(admin);
        }
    }
}
