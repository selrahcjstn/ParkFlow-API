using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        Console.WriteLine("🌱 Checking database migrations and test accounts...");
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();

        var userAccountRepository = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        var authIdentityRepository = scope.ServiceProvider.GetRequiredService<IAuthIdentityRepository>();
        var userProfileRepository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
        var adminRepository = scope.ServiceProvider.GetRequiredService<IAdminRepository>();
        var guardRepository = scope.ServiceProvider.GetRequiredService<IGuardRepository>();
        var studentRepository = scope.ServiceProvider.GetRequiredService<IStudentRepository>();
        var personnelRepository = scope.ServiceProvider.GetRequiredService<IPersonnelRepository>();
        var vehicleRepository = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        var corSubmissionRepository = scope.ServiceProvider.GetRequiredService<ICorSubmissionRepository>();
        var parkingScheduleRepository = scope.ServiceProvider.GetRequiredService<IParkingScheduleRepository>();
        var qrCodeService = scope.ServiceProvider.GetRequiredService<IQrCodeService>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // 1. SuperAdmin Account
        var superAdminEmail = "superadmin@parkflow.com";
        var superAdminPassword = passwordHasher.HashPassword("SuperAdmin123!");
        var existingSuperAdmin = await userAccountRepository.GetByEmailAsync(superAdminEmail);
        if (existingSuperAdmin == null)
        {
            var user = new UserAccount(superAdminPassword, "+639000000000");
            user.UpdateOnboardingStep(OnboardingStep.Done);
            user.Verify();
            await userAccountRepository.AddAsync(user);

            var identity = AuthIdentity.CreateManual(user.Id, superAdminEmail, superAdminPassword, isPrimary: true);
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
            existingSuperAdmin.UpdatePassword(superAdminPassword);
            await userAccountRepository.UpdateAsync(existingSuperAdmin);

            var identity = await authIdentityRepository.GetByEmailAsync(superAdminEmail);
            if (identity == null)
            {
                var newIdentity = AuthIdentity.CreateManual(existingSuperAdmin.Id, superAdminEmail, superAdminPassword, isPrimary: true);
                newIdentity.MarkVerified();
                await authIdentityRepository.AddAsync(newIdentity);
            }
            else
            {
                identity.UpdatePasswordHash(superAdminPassword);
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
        var adminEmail = "admin@parkflow.com";
        var adminPassword = passwordHasher.HashPassword("Admin123!");
        var existingAdmin = await userAccountRepository.GetByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var user = new UserAccount(adminPassword, "+639000000001");
            user.UpdateOnboardingStep(OnboardingStep.Done);
            user.Verify();
            await userAccountRepository.AddAsync(user);

            var identity = AuthIdentity.CreateManual(user.Id, adminEmail, adminPassword, isPrimary: true);
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
            existingAdmin.UpdatePassword(adminPassword);
            await userAccountRepository.UpdateAsync(existingAdmin);

            var identity = await authIdentityRepository.GetByEmailAsync(adminEmail);
            if (identity == null)
            {
                var newIdentity = AuthIdentity.CreateManual(existingAdmin.Id, adminEmail, adminPassword, isPrimary: true);
                newIdentity.MarkVerified();
                await authIdentityRepository.AddAsync(newIdentity);
            }
            else
            {
                identity.UpdatePasswordHash(adminPassword);
                identity.MarkVerified();
                await authIdentityRepository.UpdateAsync(identity);
            }
        }

        // 3. Guard Account
        var guardEmail = "guard@parkflow.com";
        var guardPassword = passwordHasher.HashPassword("Guard123!");
        var existingGuard = await userAccountRepository.GetByEmailAsync(guardEmail);
        if (existingGuard == null)
        {
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
            existingGuard.UpdatePassword(guardPassword);
            await userAccountRepository.UpdateAsync(existingGuard);

            var identity = await authIdentityRepository.GetByEmailAsync(guardEmail);
            if (identity == null)
            {
                var newIdentity = AuthIdentity.CreateManual(existingGuard.Id, guardEmail, guardPassword, isPrimary: true);
                newIdentity.MarkVerified();
                await authIdentityRepository.AddAsync(newIdentity);
            }
            else
            {
                identity.UpdatePasswordHash(guardPassword);
                identity.MarkVerified();
                await authIdentityRepository.UpdateAsync(identity);
            }
        }

        // 4. Student User Account
        var studentEmail = "student@parkflow.com";
        var existingStudent = await userAccountRepository.GetByEmailAsync(studentEmail);
        if (existingStudent == null)
        {
            var hashedPassword = passwordHasher.HashPassword("Student123!");
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
            var hashedPassword = passwordHasher.HashPassword("Personnel123!");
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
}
