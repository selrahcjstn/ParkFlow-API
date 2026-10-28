using System.Text.Json;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateManualParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Queries.VerifyStudentScan;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Features.Reservations.Commands.CancelReservation;
using ParkFlow.Application.Features.Reservations.Queries.VerifyReservationScan;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.ParkingLogs;
using Xunit;

namespace Test.Features.Reservations;

public class NoEarlyEntryGraceTests
{
    private static readonly DateTime Day = new(2026, 10, 28);
    private static DateTime At(int hour) => ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(Day, TimeSpan.FromHours(hour));

    [Theory]
    [InlineData(-1L, false)]
    [InlineData(0L, true)]
    [InlineData(1L, true)]
    [InlineData(36_000_000_000L, true)]
    [InlineData(36_000_000_001L, false)]
    public void ScheduleAndReservationWindowsUseExactManilaBoundaries(long ticks, bool expected)
    {
        var utc = At(9).AddTicks(ticks);
        var local = ParkingTimeHelper.ConvertUtcToPhilippinesTime(utc);
        var schedule = new ParkingSchedule(Guid.NewGuid(), Day.DayOfWeek, TimeSpan.FromHours(9), TimeSpan.FromHours(10));
        var reservation = new ParkingReservation(Guid.NewGuid(), "RES-BOUNDARY", Day, schedule.StartTime, schedule.EndTime, "Visit");
        Assert.Equal(new DateTime(2026, 10, 28, 1, 0, 0, DateTimeKind.Utc), At(9));
        Assert.Equal(TimeSpan.FromHours(9), new ScheduleService().GetEarliestAllowedEntryTime(schedule));
        Assert.Equal(expected, new ScheduleService().CanEnter(local, schedule));
        Assert.Equal(expected, ReservationSessionWindow.CoversEntry(reservation, utc));
    }

    [Theory]
    [InlineData(-1L, false)]
    [InlineData(0L, true)]
    [InlineData(1L, true)]
    public void MidnightReservationDoesNotLeakIntoThePreviousManilaDay(long ticks, bool expected)
    {
        var reservation = new ParkingReservation(Guid.NewGuid(), "RES-MIDNIGHT", Day, TimeSpan.Zero, TimeSpan.FromHours(1), "Visit");
        Assert.Equal(expected, ReservationSessionWindow.CoversEntry(reservation, At(0).AddTicks(ticks)));
    }

    [Theory]
    [InlineData(-1L, false)]
    [InlineData(0L, true)]
    [InlineData(1L, true)]
    [InlineData(36_000_000_000L, true)]
    [InlineData(36_000_000_001L, false)]
    public async Task ReservationScanUsesExactStartAndEnd(long ticks, bool expected)
    {
        var f = await Fixture.Create();
        var handler = new VerifyReservationScanHandler(f.Logs, f.Reservations, f.Vehicles, f.Profiles, new FrozenClock(At(9).AddTicks(ticks)));
        var result = await handler.Handle(new("RES-EXACT"), default);
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(expected, result.Data!.IsValid);
    }

    [Theory]
    [InlineData(true, -1L, false)]
    [InlineData(true, 0L, true)]
    [InlineData(true, 1L, true)]
    [InlineData(false, -1L, false)]
    [InlineData(false, 0L, true)]
    public async Task ReservationAndVehicleQrEntryRejectEvenOneTickBeforeStart(bool reservationQr, long ticks, bool expected)
    {
        var f = await Fixture.Create();
        var handler = new CreateParkingLogHandler(f.Logs, f.Vehicles, f.Profiles, f.Guards, f.Documents,
            f.Schedules, f.Students, new Test.Support.FakePersonnelRepository(), new Test.Support.FakeAdminRepository(),
            new ParkingService(), new ScheduleService(), new ParkingLogRoleService(), new FakeSignalRNotificationSender(),
            f.Reservations, timeProvider: new FrozenClock(At(9).AddTicks(ticks)));
        var result = await handler.Handle(new(reservationQr ? "RES-EXACT" : "QR-EXACT", f.GuardId), default);
        Assert.Equal(expected, result.IsSuccess);
        Assert.Equal(expected ? 1 : 0, f.Logs.Entries);
        if (!expected) Assert.Contains("Too early", result.Message);
        else
        {
            var settings = SystemSettingsStore.Current;
            Assert.Equal(At(10).AddMinutes(settings.IsGracePeriodEnabled ? settings.GracePeriodMinutes : 0), result.Data!.MaximumExitTime);
        }
    }

    [Theory]
    [InlineData(-1L, false)]
    [InlineData(0L, true)]
    public async Task ManualReservationEntryUsesTheSameStart(long ticks, bool expected)
    {
        var f = await Fixture.Create();
        var handler = new CreateManualParkingLogHandler(f.Logs, f.Vehicles, f.Profiles,
            new Test.Features.Auth.FakeUserAccountRepository(), f.Guards, f.Documents, f.Schedules,
            f.Students, new Test.Support.FakePersonnelRepository(), new Test.Support.FakeAdminRepository(),
            new ParkingService(), new ScheduleService(), new ParkingLogRoleService(), new FakeSignalRNotificationSender(),
            reservationRepository: f.Reservations, timeProvider: new FrozenClock(At(9).AddTicks(ticks)));
        var result = await handler.Handle(new("ABC123", VehicleType.Car, null, "Toyota", f.GuardId), default);
        Assert.Equal(expected, result.IsSuccess);
        Assert.Equal(expected ? 1 : 0, f.Logs.Entries);
    }

    [Theory]
    [InlineData(-1L, false)]
    [InlineData(0L, true)]
    public async Task StudentScheduleQrEntryRequiresTheActualClassStart(long ticks, bool expected)
    {
        var f = await Fixture.Create();
        f.Reservations.Reservations.Clear();
        var cor = (await f.Documents.ListCorSubmissionsAsync()).Single();
        await f.Schedules.AddAsync(new ParkingSchedule(cor.Id, Day.DayOfWeek, TimeSpan.FromHours(9), TimeSpan.FromHours(10)));
        var handler = new CreateParkingLogHandler(f.Logs, f.Vehicles, f.Profiles, f.Guards, f.Documents,
            f.Schedules, f.Students, new Test.Support.FakePersonnelRepository(), new Test.Support.FakeAdminRepository(),
            new ParkingService(), new ScheduleService(), new ParkingLogRoleService(), new FakeSignalRNotificationSender(),
            f.Reservations, timeProvider: new FrozenClock(At(9).AddTicks(ticks)));
        var result = await handler.Handle(new("QR-EXACT", f.GuardId), default);
        Assert.Equal(expected, result.IsSuccess);
        Assert.Equal(expected ? 1 : 0, f.Logs.Entries);
    }

    [Theory]
    [InlineData(-1L, false)]
    [InlineData(0L, true)]
    public async Task StudentScanDisplaysTheActualReservationStart(long ticks, bool expected)
    {
        var f = await Fixture.Create();
        var handler = new VerifyStudentScanHandler(f.Students, f.Profiles, f.Documents, f.Schedules, f.Vehicles,
            f.Logs, new ScheduleService(), f.Reservations, new FrozenClock(At(9).AddTicks(ticks)));
        var result = await handler.Handle(new(StudentNumber: "202312345"), default);
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(expected, result.Data!.IsValid);
        Assert.StartsWith("09:00", result.Data.AllowedEntryWindow);
        Assert.DoesNotContain("grace", result.Data.AllowedEntryWindow!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ReservationType.Normal, -1L, true)]
    [InlineData(ReservationType.Normal, 0L, false)]
    [InlineData(ReservationType.Normal, 1L, false)]
    [InlineData(ReservationType.Special, -1L, true)]
    [InlineData(ReservationType.Special, 0L, false)]
    public async Task CancellationCutoffIsStartOrSpecialPassMidnight(ReservationType type, long ticks, bool expected)
    {
        var f = await Fixture.Create(type);
        var cutoff = type == ReservationType.Special ? At(0) : At(9);
        var result = await new CancelReservationHandler(f.Reservations, new FakeSignalRNotificationSender(), new FrozenClock(cutoff.AddTicks(ticks)))
            .Handle(new(f.Reservation.Id, f.Reservation.UserId), default);
        Assert.Equal(expected, result.IsSuccess);
        Assert.Equal(expected ? ReservationStatus.Cancelled : ReservationStatus.Approved, f.Reservation.Status);
    }

    [Fact]
    public async Task SpecialPassKeepsItsExistingSameDayEntryPolicy()
    {
        var f = await Fixture.Create(ReservationType.Special);
        var handler = new VerifyReservationScanHandler(f.Logs, f.Reservations, f.Vehicles, f.Profiles, new FrozenClock(At(0)));
        Assert.True((await handler.Handle(new("RES-EXACT"), default)).Data!.IsValid);
        Assert.True(ReservationSessionWindow.CoversEntry(f.Reservation, At(0)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LegacyJsonCannotRestoreEarlyGraceAndExitGraceStillSerializes(bool enabled)
    {
        var settings = JsonSerializer.Deserialize<SystemSettingsDto>("""
            {"isEarlyParkingAllowed":true,"earlyParkingMinutes":120,"isGracePeriodEnabled":true,"gracePeriodMinutes":7}
            """, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var json = JsonSerializer.Serialize(settings);
        Assert.DoesNotContain("Early", json);
        Assert.True(settings.IsGracePeriodEnabled);
        Assert.Equal(7, settings.GracePeriodMinutes);
        settings.IsGracePeriodEnabled = enabled;
        var schedule = new ParkingSchedule(Guid.NewGuid(), Day.DayOfWeek, TimeSpan.FromHours(9), TimeSpan.FromHours(10));
        Assert.False(new ScheduleService().CanEnter(ParkingTimeHelper.ConvertUtcToPhilippinesTime(At(9).AddTicks(-1)), schedule));
        var deadline = At(10).AddMinutes(enabled ? 7 : 0);
        var exit = ActiveSessionTiming.ResolveCheckout(At(9), EntryMethod.QrCode, null, null, schedule, settings, deadline);
        Assert.Equal(0, exit.OverstayHours);
        var afterExitGrace = ActiveSessionTiming.ResolveCheckout(At(9), EntryMethod.QrCode, null, null, schedule, settings, deadline.AddSeconds(1));
        Assert.Equal(1d / 3600, afterExitGrace.OverstayHours, 8);
    }

    private sealed class FrozenClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class RecordingLogs : Test.Support.FakeParkingLogRepository, IParkingLogRepository
    {
        public int Entries { get; private set; }
        public new Task AddParkingLogAsync(ParkingLog log) { Entries++; ActiveParkingLog = log; return Task.CompletedTask; }
    }

    private sealed class Fixture
    {
        public RecordingLogs Logs { get; } = new();
        public FakeParkingReservationRepository Reservations { get; } = new();
        public Test.Support.FakeVehicleRepository Vehicles { get; } = new();
        public FakeTestUserProfileRepository Profiles { get; } = new();
        public Test.Support.FakeGuardRepository Guards { get; } = new();
        public FakeTestCorSubmissionRepository Documents { get; } = new();
        public FakeTestParkingScheduleRepository Schedules { get; } = new();
        public FakeTestStudentRepository Students { get; } = new();
        public Guid GuardId { get; private set; }
        public ParkingReservation Reservation { get; private set; } = null!;

        public static async Task<Fixture> Create(ReservationType type = ReservationType.Normal)
        {
            var f = new Fixture();
            var owner = new UserAccount("hash", "09123456789");
            var profile = new UserProfile(owner.Id, "Student", "One", null, "profile.jpg") { UserAccount = owner };
            profile.SubmitLicense("license.jpg"); profile.ReviewLicense(true, Guid.NewGuid(), "Approved");
            owner.UserProfile = profile;
            await f.Profiles.AddAsync(profile);
            await f.Students.AddAsync(new Student(profile.Id, "202312345", "BSCS", "1A", 1) { UserProfile = profile });
            await f.Documents.AddCorSubmissionAsync(new CorSubmission(owner.Id, "term", "cor.pdf", verificationStatus: CorVerificationStatus.Verified));
            var vehicle = new Vehicle(owner.Id, "ABC123", "Toyota", "QR-EXACT", VehicleType.Car, verificationStatus: CorVerificationStatus.Verified);
            typeof(Vehicle).GetProperty("Owner")!.SetValue(vehicle, owner);
            await f.Vehicles.AddAsync(vehicle);
            var guard = new UserProfile(Guid.NewGuid(), "Guard", "One", null, null);
            await f.Profiles.AddAsync(guard);
            f.GuardId = guard.UserAccountId;
            f.Guards.Guard = new Guard(guard, 1);
            f.Reservation = new ParkingReservation(owner.Id, "RES-EXACT", Day, TimeSpan.FromHours(9), TimeSpan.FromHours(10), "Visit", type, vehicle.Id);
            f.Reservation.Approve(Guid.NewGuid());
            await f.Reservations.AddAsync(f.Reservation);
            return f;
        }
    }
}
