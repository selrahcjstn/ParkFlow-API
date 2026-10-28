using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateManualParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.ExitManualParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.ExitParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveSessionByVehicleId;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Support;
using Xunit;

namespace Test.Features.ParkingLogs;

public class PostReservationEntryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndedReservationDoesNotBlockStandardOrScheduledManualEntry(bool completed)
    {
        var f = await Fixture.Create(completed, "valid");
        var standard = await f.StandardEntry().Handle(new(f.Vehicle.QrCodeHash, f.GuardUserId), default);
        Assert.True(standard.IsSuccess, standard.Message);
        Assert.Equal(EntryMethod.QrCode.ToString(), standard.Data!.EntryMethod);
        Assert.True(standard.Data.MaximumExitTime > DateTime.UtcNow);
        var manualFixture = await Fixture.Create(completed, "valid");
        var manual = await manualFixture.ManualEntry().Handle(manualFixture.ManualCommand(), default);
        Assert.True(manual.IsSuccess, manual.Message);
        Assert.Equal(EntryMethod.ManualScheduled.ToString(), manual.Data!.EntryMethod);
    }

    [Theory]
    [InlineData(false, "none")]
    [InlineData(true, "none")]
    [InlineData(false, "ended")]
    [InlineData(true, "ended")]
    public async Task ManualEntryAfterReservationCreatesFreeSession(bool completed, string schedule)
    {
        var f = await Fixture.Create(completed, schedule);
        var accepted = await f.ManualEntry().Handle(f.ManualCommand(), default);
        Assert.True(accepted.IsSuccess, accepted.Message);
        Assert.Equal(EntryMethod.Manual.ToString(), accepted.Data!.EntryMethod);
        Assert.True(accepted.Data.MaximumExitTime > DateTime.UtcNow);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EntryAfterReservationRetainsOperationalRules(bool completed)
    {
        var f = await Fixture.Create(completed, "valid");
        var manual = await f.ManualEntry().Handle(f.ManualCommand(), default);
        Assert.True(manual.IsSuccess, manual.Message);
        var duplicate = await f.StandardEntry().Handle(new(f.Vehicle.QrCodeHash, f.GuardUserId), default);
        Assert.False(duplicate.IsSuccess);
    }

    [Theory]
    [InlineData(EntryMethod.QrCode)]
    [InlineData(EntryMethod.ManualScheduled)]
    [InlineData(EntryMethod.Manual)]
    public async Task NewSessionAfterReservationUsesItsOwnDeadlineOnBothExitPaths(EntryMethod method)
    {
        var f = await Fixture.Create(true, "valid");
        var log = f.ActiveLog(method);
        var live = await new GetActiveSessionByVehicleIdHandler(f.Logs, f.Vehicles, f.Schedules, f.Documents, f.Reservations).Handle(new(f.Vehicle.Id), default);
        Assert.True(live.IsSuccess);
        Assert.Equal(0d, live.Data!.OverstayHours);
        Assert.True(DateTime.Parse(live.Data!.ExitBy!).ToUniversalTime() > DateTime.UtcNow);
        var response = await new ExitManualParkingLogHandler(f.Logs, f.Vehicles, f.Profiles, f.Guards,
            f.Documents, f.Schedules, f.Students, f.Personnel, f.Admins, new ParkingService(), new ParkingLogRoleService(), new ExitManualParkingLogValidator(),
            new FakeSignalRNotificationSender(), f.Reservations).Handle(new(f.Vehicle.PlateNumber, f.GuardUserId), default);
        Assert.True(response.IsSuccess, response.Message);
        Assert.Equal(0d, response.Data!.OverstayTime);
        Assert.Equal(ParkingStatus.Exited, log.Status);
        Assert.NotNull(log.ExitTime);
        Assert.Equal(DateTimeKind.Utc, log.ExitTime.Value.Kind);
        Assert.Same(log, Assert.Single(f.Logs.CompletedExits));
        Assert.Null(f.Logs.ActiveParkingLog);
        Assert.Empty(f.Logs.CompletedReservations); // An earlier booking belongs to an earlier session.

        f.ActiveLog(method);
        var qrExit = await new ExitParkingLogHandler(f.Logs, f.Vehicles, f.Profiles, f.Guards,
            f.Documents, f.Schedules, f.Students, f.Personnel, f.Admins, new ParkingService(), new ParkingLogRoleService(), new ExitParkingLogValidator(),
            new FakeSignalRNotificationSender(), f.Reservations).Handle(new(f.Vehicle.QrCodeHash, f.GuardUserId), default);
        Assert.True(qrExit.IsSuccess, qrExit.Message);
        Assert.Equal(0d, qrExit.Data!.OverstayTime);
        Assert.Equal(2, f.Logs.CompletedExits.Count);
        Assert.All(f.Logs.CompletedExits, completed => { Assert.NotNull(completed.ExitTime); Assert.Equal(ParkingStatus.Exited, completed.Status); });
        Assert.Null(f.Logs.ActiveParkingLog);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckoutPersistsCurrentReservationCompletion(bool manual)
    {
        var f = await Fixture.Create(true, "valid");
        var now = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);
        var booking = new ParkingReservation(f.Vehicle.OwnerId, "RES-CURRENT", now.Date,
            now.TimeOfDay.Subtract(TimeSpan.FromMinutes(5)), now.TimeOfDay.Add(TimeSpan.FromMinutes(30)),
            "Current visit", vehicleId: f.Vehicle.Id);
        booking.Approve(Guid.NewGuid());
        await f.Reservations.AddAsync(booking);
        var log = f.ActiveLog(EntryMethod.QrCode);
        if (manual)
        {
            var result = await new ExitManualParkingLogHandler(f.Logs, f.Vehicles, f.Profiles, f.Guards,
                f.Documents, f.Schedules, f.Students, f.Personnel, f.Admins, new ParkingService(), new ParkingLogRoleService(),
                new ExitManualParkingLogValidator(), new FakeSignalRNotificationSender(), f.Reservations)
                .Handle(new(f.Vehicle.PlateNumber, f.GuardUserId), default);
            Assert.True(result.IsSuccess, result.Message);
        }
        else
        {
            var result = await new ExitParkingLogHandler(f.Logs, f.Vehicles, f.Profiles, f.Guards,
                f.Documents, f.Schedules, f.Students, f.Personnel, f.Admins, new ParkingService(), new ParkingLogRoleService(),
                new ExitParkingLogValidator(), new FakeSignalRNotificationSender(), f.Reservations)
                .Handle(new(f.Vehicle.QrCodeHash, f.GuardUserId), default);
            Assert.True(result.IsSuccess, result.Message);
        }
        Assert.Equal(ReservationStatus.Completed, booking.Status);
        Assert.Same(booking, Assert.Single(f.Logs.CompletedReservations));
        Assert.Same(log, Assert.Single(f.Logs.CompletedExits));
        Assert.NotNull(log.ExitTime);
        Assert.Equal(ParkingStatus.Exited, log.Status);
        Assert.Null(f.Logs.ActiveParkingLog);
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(14, true)]
    [InlineData(16, false)]
    public void ReservationWindowTracksOriginalEntry_NotLaterExitOrLaterSession(int entryHour, bool covered)
    {
        var day = new DateTime(2026, 10, 10);
        var booking = new ParkingReservation(Guid.NewGuid(), "RES-TIME", day,
            TimeSpan.FromHours(10), TimeSpan.FromHours(15), "Visit");
        var utcEntry = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(day, TimeSpan.FromHours(entryHour));
        Assert.Equal(covered, ReservationSessionWindow.CoversEntry(booking, utcEntry));
        Assert.False(ReservationSessionWindow.CoversEntry(booking, utcEntry.AddDays(1)));
    }

    private sealed class Fixture
    {
        public FakeParkingLogRepository Logs { get; } = new();
        public FakeTestVehicleRepository Vehicles { get; } = new();
        public FakeTestUserProfileRepository Profiles { get; } = new();
        public FakeGuardRepository Guards { get; } = new();
        public FakeTestCorSubmissionRepository Documents { get; } = new();
        public FakeTestParkingScheduleRepository Schedules { get; } = new();
        public FakeTestStudentRepository Students { get; } = new();
        public FakePersonnelRepository Personnel { get; } = new();
        public FakeAdminRepository Admins { get; } = new();
        public FakeTestParkingReservationRepository Reservations { get; } = new();
        public Vehicle Vehicle { get; private set; } = null!;
        public Guid GuardUserId { get; private set; }

        public static async Task<Fixture> Create(bool completed, string schedule)
        {
            var f = new Fixture();
            var owner = new UserAccount("hash", "+639123456789");
            var profile = new UserProfile(owner.Id, "Student", "One", null, null) { UserAccount = owner };
            Test.Support.DriverDocumentFixture.Approve(profile);
            owner.UserProfile = profile;
            await f.Profiles.AddAsync(profile);
            await f.Students.AddAsync(new Student(profile.Id, "2023106763", "BSCS", "4A", 4) { UserProfile = profile });
            var guardProfile = new UserProfile(Guid.NewGuid(), "Guard", "One", null, null);
            await f.Profiles.AddAsync(guardProfile);
            f.GuardUserId = guardProfile.UserAccountId;
            f.Guards.Guard = new Guard(guardProfile, 1);
            f.Vehicle = new Vehicle(owner.Id, "ABC123", "Toyota", "QR-NEW-VISIT", VehicleType.Car);
            f.Vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
            typeof(Vehicle).GetProperty("Owner")!.SetValue(f.Vehicle, owner);
            await f.Vehicles.AddAsync(f.Vehicle);
            var document = new CorSubmission(owner.Id, "term", "doc.pdf", verificationStatus: CorVerificationStatus.Verified);
            await f.Documents.AddCorSubmissionAsync(document);
            var now = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);
            if (schedule != "none")
                await f.Schedules.AddAsync(new ParkingSchedule(document.Id, now.DayOfWeek, TimeSpan.Zero,
                    schedule == "valid" ? new TimeSpan(23, 59, 59) : now.TimeOfDay.Subtract(TimeSpan.FromMinutes(30))));
            var booking = new ParkingReservation(owner.Id, "RES-EARLIER", now.Date,
                now.TimeOfDay.Subtract(TimeSpan.FromHours(2)), now.TimeOfDay.Subtract(TimeSpan.FromMinutes(20)),
                "Earlier reservation", vehicleId: f.Vehicle.Id);
            booking.Approve(Guid.NewGuid());
            if (completed) booking.MarkCompleted();
            await f.Reservations.AddAsync(booking);
            return f;
        }

        public CreateManualParkingLogHandler ManualEntry() => new(Logs, Vehicles, Profiles,
            new Test.Features.Auth.FakeUserAccountRepository(), Guards, Documents, Schedules, Students,
            Personnel, Admins, new ParkingService(), new ScheduleService(), new ParkingLogRoleService(),
            new FakeSignalRNotificationSender(), reservationRepository: Reservations);
        public CreateParkingLogHandler StandardEntry() => new(Logs, Vehicles, Profiles, Guards, Documents,
            Schedules, Students, Personnel, Admins, new ParkingService(), new ScheduleService(),
            new ParkingLogRoleService(), new FakeSignalRNotificationSender(), Reservations);
        public CreateManualParkingLogCommand ManualCommand() => new(Vehicle.PlateNumber, VehicleType.Car, null, Vehicle.Brand, GuardUserId);
        public ParkingLog ActiveLog(EntryMethod method)
        {
            var log = new ParkingLog(Vehicle.Id, null, ParkingStatus.Parked, method);
            typeof(ParkingLog).GetProperty("Vehicle")!.SetValue(log, Vehicle);
            Logs.ActiveParkingLog = log;
            return log;
        }
    }
}
