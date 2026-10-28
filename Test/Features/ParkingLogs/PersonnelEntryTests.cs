using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateManualParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Auth;
using Test.Support;
using Xunit;

namespace Test.Features.ParkingLogs;

public class PersonnelEntryTests
{
    [Theory]
    [InlineData(Roles.UniversityStaff, false, true, true, true)]
    [InlineData(Roles.UniversityStaff, true, true, true, true)]
    [InlineData(Roles.NonAcademicPersonnel, false, true, true, true)]
    [InlineData(Roles.NonAcademicPersonnel, true, true, true, true)]
    [InlineData(Roles.UniversityStaff, false, false, true, false)]
    [InlineData(Roles.NonAcademicPersonnel, true, false, true, false)]
    [InlineData(Roles.UniversityStaff, true, true, false, false)]
    public async Task StaffEntryNeedsApprovedIdAndVehicleButNeverAnUploadedSchedule(
        Roles role, bool manual, bool approvedId, bool approvedVehicle, bool expected)
    {
        var owner = Guid.NewGuid();
        var guardId = Guid.NewGuid();
        var profiles = new FakeUserProfileRepository();
        var profile = new UserProfile(owner, "Employee", "One", null, null);
        Test.Support.DriverDocumentFixture.Approve(profile);
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
        var service = new ParkingService();
        var scheduleService = new ScheduleService();
        var roles = new ParkingLogRoleService();
        var notifications = new FakeSignalRNotificationSender();
        var result = manual
            ? await new CreateManualParkingLogHandler(logs, vehicles, profiles, new FakeUserAccountRepository(),
                guards, documents, schedules, students, personnel, admins, service, scheduleService, roles, notifications)
                .Handle(new CreateManualParkingLogCommand("EMP123", VehicleType.Car, "+639123456789", "Toyota", guardId), default)
            : await new CreateParkingLogHandler(logs, vehicles, profiles, guards, documents, schedules,
                students, personnel, admins, service, scheduleService, roles, notifications)
                .Handle(new CreateParkingLogCommand("EMP-HASH", guardId), default);
        Assert.Equal(expected, result.IsSuccess);
        if (expected)
        {
            Assert.Equal(role == Roles.UniversityStaff ? (DateTime?)null : PersonnelParkingPolicy.GetDeadlineUtc(result.Data!.EntryTime!.Value, SystemSettingsStore.Current), result.Data!.MaximumExitTime);
        }
        else Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
    }
}
