using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateManualParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Auth;
using Test.Features.Violations;
using Xunit;

namespace Test.Features.ParkingLogs;

public class PersonnelEntryTests
{
    [Theory]
    [InlineData(Roles.UniversityStaff, false, true, true, 0, true)]
    [InlineData(Roles.UniversityStaff, true, true, true, 0, true)]
    [InlineData(Roles.NonAcademicPersonnel, false, true, true, 0, true)]
    [InlineData(Roles.NonAcademicPersonnel, true, true, true, 0, true)]
    [InlineData(Roles.UniversityStaff, false, false, true, 0, false)]
    [InlineData(Roles.NonAcademicPersonnel, true, false, true, 0, false)]
    [InlineData(Roles.UniversityStaff, true, true, false, 0, false)]
    [InlineData(Roles.UniversityStaff, false, true, true, 1, false)]
    public async Task StaffEntryNeedsApprovedIdAndVehicleButNeverAnUploadedSchedule(
        Roles role, bool manual, bool approvedId, bool approvedVehicle, int unpaid, bool expected)
    {
        var owner = Guid.NewGuid();
        var guardId = Guid.NewGuid();
        var profiles = new FakeUserProfileRepository();
        var profile = new UserProfile(owner, "Employee", "One", null, null);
        profile.UserAccount = new UserAccount("hash", "+639123456789");
        profiles.Profiles.Add(profile);
        var guardProfile = new UserProfile(guardId, "Guard", "One", null, null);
        profiles.Profiles.Add(guardProfile);
        var guards = new FakeGuardRepository { Guard = new Guard(guardProfile, 1) };
        var personnel = new Test.Features.Users.FakePersonnelRepository();
        await personnel.AddAsync(new Personnel(profile.Id, "EMP-1", "Department", role));
        var vehicle = new Vehicle(owner, "EMP123", "Toyota", "EMP-HASH", VehicleType.Car);
        if (approvedVehicle) vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
        var vehicles = new FakeVehicleRepository();
        await vehicles.AddAsync(vehicle);
        var documents = new FakeCorSubmissionRepositoryWithMock([
            new CorSubmission(owner, "Employee ID", "https://example.test/employee-id.pdf",
                verificationStatus: approvedId ? CorVerificationStatus.Verified : CorVerificationStatus.Pending),
        ]);
        var schedules = new FakeParkingScheduleRepositoryWithMock([]);
        var logs = new FakeParkingLogRepository();
        var students = new FakeStudentRepository();
        var admins = new FakeAdminRepository();
        var violations = new FakeViolationRepository { ActiveViolationCount = unpaid };
        var service = new ParkingService();
        var scheduleService = new ScheduleService();
        var roles = new ParkingLogRoleService();
        var notifications = new FakeSignalRNotificationSender();
        var result = manual
            ? await new CreateManualParkingLogHandler(logs, vehicles, profiles, new FakeUserAccountRepository(),
                guards, documents, schedules, students, personnel, admins, violations, service, scheduleService, roles, notifications)
                .Handle(new CreateManualParkingLogCommand("EMP123", VehicleType.Car, "+639123456789", "Toyota", guardId), default)
            : await new CreateParkingLogHandler(logs, vehicles, profiles, guards, documents, schedules,
                students, personnel, admins, violations, service, scheduleService, roles, notifications)
                .Handle(new CreateParkingLogCommand("EMP-HASH", guardId), default);
        Assert.Equal(expected, result.IsSuccess);
        if (expected)
        {
            Assert.Equal(0m, result.Data!.EntryFee);
            Assert.False(result.Data.FeeOptionAvailable);
            Assert.Equal(PersonnelParkingPolicy.GetDeadlineUtc(result.Data.EntryTime!.Value, SystemSettingsStore.Current), result.Data.MaximumExitTime);
        }
        else Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
    }
}
