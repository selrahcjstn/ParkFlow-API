using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.ExitManualParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.ExitParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSession;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveSessionByVehicleId;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Violations;
using Xunit;

namespace Test.Features.ParkingLogs;

public class PersonnelSessionTests
{
    [Theory]
    [InlineData(Roles.UniversityStaff, EntryMethod.QrCode, false)]
    [InlineData(Roles.UniversityStaff, EntryMethod.ManualScheduled, true)]
    [InlineData(Roles.NonAcademicPersonnel, EntryMethod.QrCode, true)]
    [InlineData(Roles.NonAcademicPersonnel, EntryMethod.ManualScheduled, false)]
    public async Task OvernightLiveViewsAndCheckoutUseTheSameOriginalCutoff(Roles role, EntryMethod method, bool manualExit)
    {
        var ownerId = Guid.NewGuid();
        var guardId = Guid.NewGuid();
        var owner = new UserAccount("hash", "+639123456789");
        var profile = new UserProfile(ownerId, "Employee", "One", null, null) { UserAccount = owner };
        owner.UserProfile = profile;
        var employee = new Personnel(profile.Id, "EMP-1", "Office", role);
        profile.Personnel = employee;
        var personnel = new Test.Features.Users.FakePersonnelRepository();
        await personnel.AddAsync(employee);
        var profiles = new FakeUserProfileRepository();
        profiles.Profiles.Add(profile);
        var guardProfile = new UserProfile(guardId, "Guard", "One", null, null);
        profiles.Profiles.Add(guardProfile);
        var guards = new FakeGuardRepository { Guard = new Guard(guardProfile, 1) };
        var vehicle = new Vehicle(ownerId, "EMP123", "Toyota", "EMP-HASH", VehicleType.Car);
        typeof(Vehicle).GetProperty("Owner")!.SetValue(vehicle, owner);
        var vehicles = new FakeVehicleRepository();
        await vehicles.AddAsync(vehicle);
        var log = new ParkingLog(vehicle.Id, guardProfile.Id, ParkingStatus.Parked, method);
        var localYesterday = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow).Date.AddDays(-1);
        var entry = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(localYesterday, TimeSpan.FromHours(8));
        typeof(ParkingLog).GetProperty("EntryTime")!.SetValue(log, entry);
        typeof(ParkingLog).GetProperty("Vehicle")!.SetValue(log, vehicle);
        var logs = new FakeParkingLogRepositoryForExit(log);
        var documents = new FakeCorSubmissionRepositoryWithMock([]);
        var schedules = new FakeParkingScheduleRepositoryWithMock([]);
        var violationService = new ViolationService();
        var roleService = new ParkingLogRoleService();
        var admins = new FakeAdminRepository();
        var students = new FakeStudentRepository();
        var settings = SystemSettingsStore.Current;
        var before = PersonnelParkingPolicy.CalculateCharge(entry, DateTime.UtcNow, settings);
        var deadline = PersonnelParkingPolicy.GetDeadlineUtc(entry, settings);

        var individual = await new GetActiveSessionByVehicleIdHandler(logs, vehicles, schedules, documents,
            violationService, userProfileRepository: profiles, personnelRepository: personnel)
            .Handle(new GetActiveSessionByVehicleIdQuery(vehicle.Id), default);
        Assert.True(individual.IsSuccess);
        Assert.Equal(deadline.ToString("yyyy-MM-ddTHH:mm:ssZ"), individual.Data!.ExitBy);
        Assert.InRange(individual.Data.AccruedCharge, before, PersonnelParkingPolicy.CalculateCharge(entry, DateTime.UtcNow, settings));

        var all = await new GetActiveParkingSessionHandler(new FakeParkingLogRepositoryWithActiveLogs([log]),
            schedules, documents, violationService, admins, roleService)
            .Handle(new GetActiveParkingSessionQuery(500), default);
        Assert.True(all.IsSuccess);
        var live = Assert.Single(all.Data!);
        Assert.Equal(deadline, live.MaximumExitTime);
        Assert.InRange(live.Amount, before, PersonnelParkingPolicy.CalculateCharge(entry, DateTime.UtcNow, settings));

        var notifications = new FakeSignalRNotificationSender();
        var result = manualExit
            ? await new ExitManualParkingLogHandler(logs, vehicles, profiles, guards, documents, schedules,
                students, personnel, admins, new FakeViolationRepository(), new ParkingService(), violationService,
                roleService, new ExitManualParkingLogValidator(), notifications)
                .Handle(new ExitManualParkingLogCommand("EMP123", guardId), default)
            : await new ExitParkingLogHandler(logs, vehicles, profiles, guards, documents, schedules,
                students, personnel, admins, new FakeViolationRepository(), new ParkingService(), violationService,
                roleService, new ExitParkingLogValidator(), notifications, new FakeTestParkingReservationRepository())
                .Handle(new ExitParkingLogCommand("EMP-HASH", guardId), default);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.InRange(result.Data.PenaltyFee, before, PersonnelParkingPolicy.CalculateCharge(entry, DateTime.UtcNow, settings));
        Assert.NotNull(result.Data.ReferenceNumber);
        Assert.Equal(ParkingStatus.Exited, log.Status);
    }
}
