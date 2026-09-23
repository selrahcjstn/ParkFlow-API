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
        var existingSuperAdmin = await userAccountRepository.GetByEmailAsync(superAdminEmail);
        if (existingSuperAdmin == null)
        {
            var hashedPassword = passwordHasher.HashPassword("SuperAdmin123!");
            var user = new UserAccount(hashedPassword, "+639000000000");
            user.UpdateOnboardingStep(OnboardingStep.Done);
            user.Verify();
            await userAccountRepository.AddAsync(user);

            var identity = AuthIdentity.CreateManual(user.Id, superAdminEmail, hashedPassword, isPrimary: true);
            identity.MarkVerified();
            await authIdentityRepository.AddAsync(identity);

            var profile = new UserProfile(user.Id, "Super", "Admin", "System", null);
            await userProfileRepository.AddAsync(profile);

            var admin = new Admin(profile, RoleLevel.SuperAdmin);
            await adminRepository.AddAsync(admin);
        }

        // 2. Regular Admin Account
        var adminEmail = "admin@parkflow.com";
        var existingAdmin = await userAccountRepository.GetByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var hashedPassword = passwordHasher.HashPassword("Admin123!");
            var user = new UserAccount(hashedPassword, "+639000000001");
            user.UpdateOnboardingStep(OnboardingStep.Done);
            user.Verify();
            await userAccountRepository.AddAsync(user);

            var identity = AuthIdentity.CreateManual(user.Id, adminEmail, hashedPassword, isPrimary: true);
            identity.MarkVerified();
            await authIdentityRepository.AddAsync(identity);

            var profile = new UserProfile(user.Id, "ParkFlow", "Admin", null, null);
            await userProfileRepository.AddAsync(profile);

            var admin = new Admin(profile, RoleLevel.Admin);
            await adminRepository.AddAsync(admin);
        }

        // 3. Guard Account
        var guardEmail = "guard@parkflow.com";
        var existingGuard = await userAccountRepository.GetByEmailAsync(guardEmail);
        if (existingGuard == null)
        {
            var hashedPassword = passwordHasher.HashPassword("Guard123!");
            var user = new UserAccount(hashedPassword, "+639000000002");
            user.UpdateOnboardingStep(OnboardingStep.Done);
            user.Verify();
            await userAccountRepository.AddAsync(user);

            var identity = AuthIdentity.CreateManual(user.Id, guardEmail, hashedPassword, isPrimary: true);
            identity.MarkVerified();
            await authIdentityRepository.AddAsync(identity);

            var profile = new UserProfile(user.Id, "Campus", "Guard", "Security", null);
            await userProfileRepository.AddAsync(profile);

            var guard = new Guard(profile, assignedGate: 1);
            await guardRepository.AddAsync(guard);
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
    }
}
