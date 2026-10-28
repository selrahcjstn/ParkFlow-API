using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ParkFlow.Persistence;

public static class SuperAdminSeeder
{
    public static async Task SeedSuperAdminAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("🌱 Checking database readiness and administrator accounts...");
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseStartupCheck.EnsureReadyAsync(dbContext);

        var userAccountRepository = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        var authIdentityRepository = scope.ServiceProvider.GetRequiredService<IAuthIdentityRepository>();
        var userProfileRepository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var adminRepository = scope.ServiceProvider.GetRequiredService<IAdminRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var includeTestData = bool.TryParse(configuration["Seeding:IncludeTestData"], out var enabled) && enabled;
        if (includeTestData && scope.ServiceProvider.GetService<IHostEnvironment>()?.IsDevelopment() != true)
            throw new InvalidOperationException("Seeding:IncludeTestData requires an explicit Development host. No sample accounts were provisioned.");

        // 1. SuperAdmin Account
        var superAdminEmail = BootstrapEmail(configuration, "SuperAdmin");
        var existingSuperAdmin = await userAccountRepository.GetByEmailAsync(superAdminEmail);
        var adminEmail = BootstrapEmail(configuration, "Admin");
        var existingAdmin = await userAccountRepository.GetByEmailAsync(adminEmail);
        // Rename the original seeded account instead of deleting its profile/history
        // or creating another account with elevated permissions.
        var previousEmail = configuration["Seeding:SuperAdmin:PreviousEmail"];
        var previousSuperAdmin = string.IsNullOrWhiteSpace(previousEmail) || string.Equals(superAdminEmail, previousEmail, StringComparison.OrdinalIgnoreCase)
            ? null : await userAccountRepository.GetByEmailAsync(previousEmail);
        // Validate missing-account secrets before performing any account writes.
        var superAdminPassword = existingSuperAdmin?.PasswordHash ?? previousSuperAdmin?.PasswordHash;
        if (existingSuperAdmin == null && previousSuperAdmin == null)
            superAdminPassword = BootstrapPassword(configuration, "SuperAdmin", passwordHasher);
        var adminPassword = existingAdmin?.PasswordHash;
        if (existingAdmin == null)
            adminPassword = BootstrapPassword(configuration, "Admin", passwordHasher);
        if (previousSuperAdmin != null)
        {
            if (existingSuperAdmin != null)
                throw new InvalidOperationException("Both Super Admin emails exist. Resolve the account conflict before seeding.");
            if (previousSuperAdmin.UserProfile?.Admin?.RoleLevel != RoleLevel.SuperAdmin)
                throw new InvalidOperationException("The previous Super Admin email no longer belongs to a Super Admin account.");

            var previousIdentity = await authIdentityRepository.GetByEmailAsync(previousEmail!)
                ?? throw new InvalidOperationException("The previous Super Admin login identity was not found.");
            if (previousIdentity.Provider != AuthProvider.Manual)
                throw new InvalidOperationException("The previous Super Admin login is not a manual login identity.");
            previousIdentity.UpdateEmail(superAdminEmail);
            await authIdentityRepository.UpdateAsync(previousIdentity);
            existingSuperAdmin = previousSuperAdmin;
        }
        if (existingSuperAdmin == null)
        {
            var user = new UserAccount(superAdminPassword!, "+639000000000");
            user.UpdateOnboardingStep(OnboardingStep.Done);
            user.Verify();
            await userAccountRepository.AddAsync(user);

            var identity = AuthIdentity.CreateManual(user.Id, superAdminEmail, superAdminPassword!, isPrimary: true);
            identity.MarkVerified();
            await authIdentityRepository.AddAsync(identity);

            var profile = new UserProfile(user.Id, "Super", "Admin", "System", null);
            await userProfileRepository.AddAsync(profile);

            var admin = new Admin(profile, RoleLevel.SuperAdmin);
            await adminRepository.AddAsync(admin);
        }
        else
        {
            existingSuperAdmin.Verify();
            existingSuperAdmin.UpdateOnboardingStep(OnboardingStep.Done);
            await userAccountRepository.UpdateAsync(existingSuperAdmin);

            var identity = await authIdentityRepository.GetByEmailAsync(superAdminEmail);
            if (identity == null)
            {
                var newIdentity = AuthIdentity.CreateManual(existingSuperAdmin.Id, superAdminEmail, superAdminPassword ?? throw new InvalidOperationException("Existing Super Admin has no password hash. Repair its identity through the approved account recovery process."), isPrimary: true);
                newIdentity.MarkVerified();
                await authIdentityRepository.AddAsync(newIdentity);
            }
            else
            {
                identity.MarkVerified();
                await authIdentityRepository.UpdateAsync(identity);
            }

            var profile = await userProfileRepository.GetByUserIdAsync(existingSuperAdmin.Id);
            if (profile == null)
            {
                profile = new UserProfile(existingSuperAdmin.Id, "Super", "Admin", "System", null);
                await userProfileRepository.AddAsync(profile);
            }

            var admin = await adminRepository.GetByUserProfileIdAsync(profile.Id);
            if (admin == null)
            {
                admin = new Admin(profile, RoleLevel.SuperAdmin);
                await adminRepository.AddAsync(admin);
            }
            else if (admin.RoleLevel != RoleLevel.SuperAdmin)
            {
                admin.RoleLevel = RoleLevel.SuperAdmin;
                await dbContext.SaveChangesAsync();
            }
        }

        // 2. Regular Admin Account
        if (existingAdmin == null)
        {
            var user = new UserAccount(adminPassword!, "+639000000001");
            user.UpdateOnboardingStep(OnboardingStep.Done);
            user.Verify();
            await userAccountRepository.AddAsync(user);

            var identity = AuthIdentity.CreateManual(user.Id, adminEmail, adminPassword!, isPrimary: true);
            identity.MarkVerified();
            await authIdentityRepository.AddAsync(identity);

            var profile = new UserProfile(user.Id, "ParkFlow", "Admin", null, null);
            await userProfileRepository.AddAsync(profile);

            var admin = new Admin(profile, RoleLevel.Admin);
            await adminRepository.AddAsync(admin);
        }
        else
        {
            existingAdmin.Verify();
            existingAdmin.UpdateOnboardingStep(OnboardingStep.Done);
            await userAccountRepository.UpdateAsync(existingAdmin);

            var identity = await authIdentityRepository.GetByEmailAsync(adminEmail);
            if (identity == null)
            {
                var newIdentity = AuthIdentity.CreateManual(existingAdmin.Id, adminEmail, adminPassword ?? throw new InvalidOperationException("Existing Admin has no password hash. Repair its identity through the approved account recovery process."), isPrimary: true);
                newIdentity.MarkVerified();
                await authIdentityRepository.AddAsync(newIdentity);
            }
            else
            {
                identity.MarkVerified();
                await authIdentityRepository.UpdateAsync(identity);
            }
        }

        // Sample records must be explicitly enabled; normal restarts must not
        // repopulate a cleared database or overwrite existing administrator passwords.
        if (!includeTestData)
        {
            Console.WriteLine("✅ Administrator accounts verified. Test data seeding is disabled.");
            return;
        }

        var guardRepository = scope.ServiceProvider.GetRequiredService<IGuardRepository>();
        var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
        var personnelRepository = scope.ServiceProvider.GetRequiredService<IPersonnelRepository>();
        var vehicleRepository = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var corSubmissionRepository = scope.ServiceProvider.GetRequiredService<ICorSubmissionRepository>();
        var parkingScheduleRepository = scope.ServiceProvider.GetRequiredService<IParkingScheduleRepository>();
        var qrCodeService = scope.ServiceProvider.GetRequiredService<IQrCodeService>();

        // 3. Guard Account
        var guardEmail = BootstrapEmail(configuration, "Guard");
        var guardPassword = (string?)null;
        var existingGuard = await userAccountRepository.GetByEmailAsync(guardEmail);
        if (existingGuard == null)
        {
            guardPassword = BootstrapPassword(configuration, "Guard", passwordHasher);
            var user = new UserAccount(guardPassword, "+639000000002");
            user.UpdateOnboardingStep(OnboardingStep.Done);
            user.Verify();
            await userAccountRepository.AddAsync(user);

            var identity = AuthIdentity.CreateManual(user.Id, guardEmail, guardPassword, isPrimary: true);
            identity.MarkVerified();
            await authIdentityRepository.AddAsync(identity);

            var profile = new UserProfile(user.Id, "Campus", "Guard", "Security", null);
            await userProfileRepository.AddAsync(profile);

            var guard = new Guard(profile, assignedGate: 1);
            await guardRepository.AddAsync(guard);
        }
        else
        {
            existingGuard.Verify();
            existingGuard.UpdateOnboardingStep(OnboardingStep.Done);
            await userAccountRepository.UpdateAsync(existingGuard);

            var identity = await authIdentityRepository.GetByEmailAsync(guardEmail);
            if (identity == null)
            {
                var newIdentity = AuthIdentity.CreateManual(existingGuard.Id, guardEmail, existingGuard.PasswordHash ?? throw new InvalidOperationException("Existing Guard requires approved account recovery."), isPrimary: true);
                newIdentity.MarkVerified();
                await authIdentityRepository.AddAsync(newIdentity);
            }
            else
            {
                identity.MarkVerified();
                await authIdentityRepository.UpdateAsync(identity);
            }
        }

        // 4. Student User Account
        var studentEmail = "student@parkflow.com";
        var existingStudent = await userAccountRepository.GetByEmailAsync(studentEmail);
        if (existingStudent == null)
        {
            var hashedPassword = passwordHasher.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            var user = new UserAccount(hashedPassword, "+639171234567");
            user.UpdateOnboardingStep(OnboardingStep.Done);
            user.Verify();
            await userAccountRepository.AddAsync(user);

            var identity = AuthIdentity.CreateManual(user.Id, studentEmail, hashedPassword, isPrimary: true);
            identity.MarkVerified();
            await authIdentityRepository.AddAsync(identity);

            var profile = new UserProfile(user.Id, "Juan", "Cruz", "Dela", null);
            await userProfileRepository.AddAsync(profile);

            var student = new Student(profile.Id, "2024-00001", "BSCS", "4A", 4);
            await studentRepository.AddAsync(student);

            // Seed Approved COR Submission
            var cor = new CorSubmission(
                user.Id,
                "2024-2025",
                "https://parkflow.blob.core.windows.net/documents/sample_cor.pdf",
                "https://parkflow.blob.core.windows.net/documents/sample_orcr.pdf",
                "https://parkflow.blob.core.windows.net/documents/sample_vehicle.jpg",
                CorVerificationStatus.Verified);
            await corSubmissionRepository.AddCorSubmissionAsync(cor);

            // Seed Parking Schedule
            var schedules = new List<ParkingSchedule>
            {
                new ParkingSchedule(cor.Id, DayOfWeek.Monday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
                new ParkingSchedule(cor.Id, DayOfWeek.Tuesday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
                new ParkingSchedule(cor.Id, DayOfWeek.Wednesday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
                new ParkingSchedule(cor.Id, DayOfWeek.Thursday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
                new ParkingSchedule(cor.Id, DayOfWeek.Friday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0))
            };
            await parkingScheduleRepository.ReplaceSchedulesAsync(cor.Id, schedules);

            // Seed Vehicle
            var qrPayload = $"{user.Id}:ABC-1234:Toyota";
            var qrBytes = qrCodeService.GenerateQrCode(qrPayload);
            var qrHash = Convert.ToBase64String(SHA256.HashData(qrBytes));
            var vehicle = new Vehicle(user.Id, "ABC-1234", "Toyota", qrHash, VehicleType.Motorcycle);
            vehicle.SetPrimary(true);
            vehicle.UpdateDocuments("https://parkflow.blob.core.windows.net/documents/sample_orcr.pdf", "https://parkflow.blob.core.windows.net/documents/sample_vehicle.jpg");
            vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
            await vehicleRepository.AddAsync(vehicle);
        }

        // 5. Personnel User Account
        var personnelEmail = "personnel@parkflow.com";
        var existingPersonnel = await userAccountRepository.GetByEmailAsync(personnelEmail);
        if (existingPersonnel == null)
        {
            var hashedPassword = passwordHasher.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            var user = new UserAccount(hashedPassword, "+639179876543");
            user.UpdateOnboardingStep(OnboardingStep.Done);
            user.Verify();
            await userAccountRepository.AddAsync(user);

            var identity = AuthIdentity.CreateManual(user.Id, personnelEmail, hashedPassword, isPrimary: true);
            identity.MarkVerified();
            await authIdentityRepository.AddAsync(identity);

            var profile = new UserProfile(user.Id, "Maria", "Santos", "Clara", null);
            await userProfileRepository.AddAsync(profile);

            var personnel = new Personnel(profile.Id, "EMP-2024-001", "Information Technology");
            await personnelRepository.AddAsync(personnel);

            // Seed Approved COR Submission
            var cor = new CorSubmission(
                user.Id,
                "2024-2025",
                "https://parkflow.blob.core.windows.net/documents/sample_id.pdf",
                "https://parkflow.blob.core.windows.net/documents/sample_orcr.pdf",
                "https://parkflow.blob.core.windows.net/documents/sample_vehicle.jpg",
                CorVerificationStatus.Verified);
            await corSubmissionRepository.AddCorSubmissionAsync(cor);

            // Seed Parking Schedule
            var schedules = new List<ParkingSchedule>
            {
                new ParkingSchedule(cor.Id, DayOfWeek.Monday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
                new ParkingSchedule(cor.Id, DayOfWeek.Tuesday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
                new ParkingSchedule(cor.Id, DayOfWeek.Wednesday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
                new ParkingSchedule(cor.Id, DayOfWeek.Thursday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),
                new ParkingSchedule(cor.Id, DayOfWeek.Friday, new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0))
            };
            await parkingScheduleRepository.ReplaceSchedulesAsync(cor.Id, schedules);

            // Seed Vehicle
            var qrPayload = $"{user.Id}:XYZ-5678:Honda";
            var qrBytes = qrCodeService.GenerateQrCode(qrPayload);
            var qrHash = Convert.ToBase64String(SHA256.HashData(qrBytes));
            var vehicle = new Vehicle(user.Id, "XYZ-5678", "Honda", qrHash, VehicleType.Motorcycle);
            vehicle.SetPrimary(true);
            vehicle.UpdateDocuments("https://parkflow.blob.core.windows.net/documents/sample_orcr.pdf", "https://parkflow.blob.core.windows.net/documents/sample_vehicle.jpg");
            vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
            await vehicleRepository.AddAsync(vehicle);
        }

        // 6. Seed 50 Test Accounts with Active Reservations
        await TestDataSeeder.SeedTestAccountsAndReservationsAsync(serviceProvider);
        Console.WriteLine("✅ Database and test accounts verified.");
    }
    private static string BootstrapEmail(IConfiguration configuration, string account)
    {
        var key = $"Seeding:{account}:Email";
        var email = configuration[key]?.Trim();
        if (!System.Net.Mail.MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw new InvalidOperationException($"Missing or invalid bootstrap configuration {key}. Configure the existing administrator's email; accounts are never matched or merged by name.");
        return email!;
    }

    private static string BootstrapPassword(IConfiguration configuration, string account, IPasswordHasher hasher)
    {
        var key = $"Seeding:{account}:Password";
        var password = configuration[key];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new InvalidOperationException($"Missing or invalid bootstrap secret {key}. Inject a unique password of at least 12 characters through secure configuration; existing passwords are never reset.");
        return hasher.HashPassword(password);
    }

}
