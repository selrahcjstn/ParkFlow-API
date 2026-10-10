using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateManualParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.ExitManualParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.ExitParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveSessionByVehicleId;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Violations;
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
        Assert.Equal(0m, standard.Data.EntryFee);
        Assert.True(standard.Data.MaximumExitTime > DateTime.UtcNow);
        var manual = await f.ManualEntry().Handle(f.ManualCommand(), default);
        Assert.True(manual.IsSuccess, manual.Message);
        Assert.Equal(EntryMethod.ManualScheduled.ToString(), manual.Data!.EntryMethod);
        Assert.Equal(0m, manual.Data.EntryFee);
    }

    [Theory]
    [InlineData(false, "none")]
    [InlineData(true, "none")]
    [InlineData(false, "ended")]
    [InlineData(true, "ended")]
    public async Task ManualEntryAfterReservationOffersFee_ThenAllowsConfirmedPaidSession(bool completed, string schedule)
    {
        var f = await Fixture.Create(completed, schedule);
        var offer = await f.ManualEntry().Handle(f.ManualCommand(), default);
        Assert.False(offer.IsSuccess);
        Assert.True(offer.Data!.FeeOptionAvailable);
        Assert.Equal(20m, offer.Data.EntryFee);
        Assert.DoesNotContain("Re-entry is not permitted", offer.Message);
        var accepted = await f.ManualEntry().Handle(f.ManualCommand() with { AcceptUnscheduledFee = true }, default);
        Assert.True(accepted.IsSuccess, accepted.Message);
        Assert.Equal(EntryMethod.Manual.ToString(), accepted.Data!.EntryMethod);
        Assert.Equal(20m, accepted.Data.EntryFee);
        Assert.True(accepted.Data.MaximumExitTime > DateTime.UtcNow);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnpaidChargesStillRequireSettlementBeforeEitherEntry(bool completed)
    {
        var f = await Fixture.Create(completed, "valid");
        f.Violations.ActiveViolationCount = 1;
        var manual = await f.ManualEntry().Handle(f.ManualCommand() with { AcceptUnscheduledFee = true }, default);
        var standard = await f.StandardEntry().Handle(new(f.Vehicle.QrCodeHash, f.GuardUserId), default);
        Assert.False(manual.IsSuccess);
        Assert.False(standard.IsSuccess);
        Assert.Contains("settle", manual.Message);
        Assert.Contains("settle", standard.Message);
    }

    [Theory]
    [InlineData(EntryMethod.QrCode)]
    [InlineData(EntryMethod.ManualScheduled)]
    [InlineData(EntryMethod.Manual)]
    public async Task NewSessionAfterReservationUsesItsOwnDeadlineAndFeeOnBothExitPaths(EntryMethod method)
    {
        var f = await Fixture.Create(true, "valid");
        var log = f.ActiveLog(method);
        var live = await new GetActiveSessionByVehicleIdHandler(f.Logs, f.Vehicles, f.Schedules, f.Documents,
            new FakeViolationService(), f.Reservations).Handle(new(f.Vehicle.Id), default);
        Assert.True(live.IsSuccess);
        Assert.Equal(method == EntryMethod.Manual ? 20m : 0m, live.Data!.AccruedCharge);
        Assert.Equal(0d, live.Data.OverstayHours);
        Assert.True(DateTime.Parse(live.Data.ExitBy!).ToUniversalTime() > DateTime.UtcNow);
        var response = await new ExitManualParkingLogHandler(f.Logs, f.Vehicles, f.Profiles, f.Guards,
            f.Documents, f.Schedules, f.Students, f.Personnel, f.Admins, f.Violations, new ParkingService(),
            new FakeViolationService(), new ParkingLogRoleService(), new ExitManualParkingLogValidator(),
            new FakeSignalRNotificationSender(), f.Reservations).Handle(new(f.Vehicle.PlateNumber, f.GuardUserId), default);
        Assert.True(response.IsSuccess, response.Message);
        Assert.Equal(method == EntryMethod.Manual ? 20m : 0m, response.Data!.PenaltyFee);
        Assert.Equal(0d, response.Data.OverstayTime);

        f.ActiveLog(method);
        var qrExit = await new ExitParkingLogHandler(f.Logs, f.Vehicles, f.Profiles, f.Guards,
            f.Documents, f.Schedules, f.Students, f.Personnel, f.Admins, f.Violations, new ParkingService(),
            new FakeViolationService(), new ParkingLogRoleService(), new ExitParkingLogValidator(),
            new FakeSignalRNotificationSender(), f.Reservations).Handle(new(f.Vehicle.QrCodeHash, f.GuardUserId), default);
        Assert.True(qrExit.IsSuccess, qrExit.Message);
        Assert.Equal(method == EntryMethod.Manual ? 20m : 0m, qrExit.Data!.PenaltyFee);
        Assert.Equal(0d, qrExit.Data.OverstayTime);
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
        Assert.Equal(covered, ReservationSessionWindow.CoversEntry(booking, utcEntry, new SystemSettingsDto()));
        Assert.False(ReservationSessionWindow.CoversEntry(booking, utcEntry.AddDays(1), new SystemSettingsDto()));
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
        public FakeViolationRepository Violations { get; } = new();
        public FakeTestParkingReservationRepository Reservations { get; } = new();
        public Vehicle Vehicle { get; private set; } = null!;
        public Guid GuardUserId { get; private set; }

        public static async Task<Fixture> Create(bool completed, string schedule)
        {
            var f = new Fixture();
            var owner = new UserAccount("hash", "+639123456789");
            var profile = new UserProfile(owner.Id, "Student", "One", null, null) { UserAccount = owner };
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
            Personnel, Admins, Violations, new ParkingService(), new ScheduleService(), new ParkingLogRoleService(),
            new FakeSignalRNotificationSender(), reservationRepository: Reservations);
        public CreateParkingLogHandler StandardEntry() => new(Logs, Vehicles, Profiles, Guards, Documents,
            Schedules, Students, Personnel, Admins, Violations, new ParkingService(), new ScheduleService(),
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
