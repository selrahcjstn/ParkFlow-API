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

public class EarlyStudentManualEntryTests
{
    [Fact]
    public async Task EarlyManualEntryRemainsAnEligibleExceptionWithoutACharge()
    {
        var f = await Fixture.Create();
        var accepted = await f.ManualEntry().Handle(f.Command(), default);
        Assert.True(accepted.IsSuccess, accepted.Message);
        Assert.Equal(0m, accepted.Data!.EntryFee);
        Assert.False(accepted.Data.FeeOptionAvailable);
        Assert.Equal(EntryMethod.Manual.ToString(), accepted.Data.EntryMethod);
        Assert.Equal(EntryMethod.Manual, f.Logs.ActiveParkingLog!.EntryMethod);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthorizedScheduleEntryStaysFree_EvenWithFeeConsent(bool acceptFee)
    {
        var f = await Fixture.Create(canEnter: true);
        var result = await f.ManualEntry().Handle(f.Command() with { AcceptUnscheduledFee = acceptFee }, default);
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(0m, result.Data!.EntryFee);
        Assert.Equal(EntryMethod.ManualScheduled.ToString(), result.Data.EntryMethod);
    }

    [Theory]
    [InlineData("vehicle")]
    [InlineData("document")]
    [InlineData("parked")]
    public async Task FeeConsentDoesNotBypassExistingRestrictions(string restriction)
    {
        var f = await Fixture.Create();
        if (restriction == "unpaid") f.Violations.ActiveViolationCount = 1;
        if (restriction == "vehicle") f.Vehicle.UpdateVerificationStatus(CorVerificationStatus.Pending);
        if (restriction == "document") f.Document.UpdateSubmission(null, null, CorVerificationStatus.Pending);
        if (restriction == "parked") f.Logs.ActiveParkingLog = new ParkingLog(f.Vehicle.Id, null, ParkingStatus.Parked);
        var previousLog = f.Logs.ActiveParkingLog;
        var result = await f.ManualEntry().Handle(f.Command() with { AcceptUnscheduledFee = true }, default);
        Assert.False(result.IsSuccess);
        Assert.False(result.Data?.FeeOptionAvailable ?? false);
        Assert.Same(previousLog, f.Logs.ActiveParkingLog);
    }

    [Fact]
    public async Task QrEntryBeforeScheduleIsStillRejected()
    {
        var f = await Fixture.Create();
        var handler = new CreateParkingLogHandler(f.Logs, f.Vehicles, f.Profiles, f.Guards, f.Documents,
            f.Schedules, f.Students, f.Personnel, f.Admins, f.Violations, new ParkingService(),
            f.SchedulePolicy, new ParkingLogRoleService(), new FakeSignalRNotificationSender());
        var result = await handler.Handle(new(f.Vehicle.QrCodeHash, f.GuardUserId), default);
        Assert.False(result.IsSuccess);
        Assert.Contains("Too early", result.Message);
        Assert.Null(f.Logs.ActiveParkingLog);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiveSessionAndBothExitPathsRemainFree(bool qrExit)
    {
        var f = await Fixture.Create();
        var entry = await f.ManualEntry().Handle(f.Command() with { AcceptUnscheduledFee = true }, default);
        Assert.True(entry.IsSuccess, entry.Message);
        typeof(ParkingLog).GetProperty("Vehicle")!.SetValue(f.Logs.ActiveParkingLog, f.Vehicle);

        // Starting the class window must not introduce a charge to the same session.
        f.SchedulePolicy.CanEnterNow = true;
        var live = await new GetActiveSessionByVehicleIdHandler(f.Logs, f.Vehicles, f.Schedules,
            f.Documents, new FakeViolationService()).Handle(new(f.Vehicle.Id), default);
        Assert.True(live.IsSuccess, live.Message);
        Assert.Equal(0m, live.Data!.AccruedCharge);

        var exit = qrExit
            ? await new ExitParkingLogHandler(f.Logs, f.Vehicles, f.Profiles, f.Guards, f.Documents,
                f.Schedules, f.Students, f.Personnel, f.Admins, f.Violations, new ParkingService(),
                new FakeViolationService(), new ParkingLogRoleService(), new ExitParkingLogValidator(),
                new FakeSignalRNotificationSender(), new FakeTestParkingReservationRepository())
                .Handle(new(f.Vehicle.QrCodeHash, f.GuardUserId), default)
            : await new ExitManualParkingLogHandler(f.Logs, f.Vehicles, f.Profiles, f.Guards, f.Documents,
                f.Schedules, f.Students, f.Personnel, f.Admins, f.Violations, new ParkingService(),
                new FakeViolationService(), new ParkingLogRoleService(), new ExitManualParkingLogValidator(),
                new FakeSignalRNotificationSender()).Handle(new(f.Vehicle.PlateNumber, f.GuardUserId), default);
        Assert.True(exit.IsSuccess, exit.Message);
        Assert.Equal(0m, exit.Data!.PenaltyFee);
        Assert.Equal("NotRequired", exit.Data.SettlementStatus);
        Assert.Empty(f.Violations.Violations);
    }

    [Fact]
    public void NoonScheduleDoesNotAuthorizeTenAm_UnlessEarlyBufferAllowsIt()
    {
        var schedule = new ParkingSchedule(Guid.NewGuid(), DayOfWeek.Monday, TimeSpan.FromHours(12), TimeSpan.FromHours(15));
        var policy = new ScheduleService();
        var tenAm = new DateTime(2026, 10, 12, 10, 0, 0);
        Assert.False(policy.CanEnter(tenAm, schedule, earlyBufferMinutes: 0));
        Assert.True(policy.CanEnter(tenAm.AddHours(2), schedule, earlyBufferMinutes: 0));
        Assert.True(policy.CanEnter(tenAm, schedule, earlyBufferMinutes: 120));
    }

    [Fact]
    public async Task HistoricalBalancesDoNotBlockVerifiedManualOrQrEntry()
    {
        var f = await Fixture.Create(canEnter: true);
        f.Violations.ActiveViolationCount = 4;
        var manual = await f.ManualEntry().Handle(f.Command(), default);
        Assert.True(manual.IsSuccess, manual.Message);
        f.Logs.ActiveParkingLog = null;
        var qr = await new CreateParkingLogHandler(f.Logs, f.Vehicles, f.Profiles, f.Guards, f.Documents,
            f.Schedules, f.Students, f.Personnel, f.Admins, f.Violations, new ParkingService(),
            f.SchedulePolicy, new ParkingLogRoleService(), new FakeSignalRNotificationSender())
            .Handle(new(f.Vehicle.QrCodeHash, f.GuardUserId), default);
        Assert.True(qr.IsSuccess, qr.Message);
        Assert.Equal(4, f.Violations.ActiveViolationCount);
        Assert.Empty(f.Violations.Violations);
    }

    private sealed class Fixture
    {
        public RecordingParkingLogRepository Logs { get; } = new();
        public FakeTestVehicleRepository Vehicles { get; } = new();
        public FakeTestUserProfileRepository Profiles { get; } = new();
        public FakeGuardRepository Guards { get; } = new();
        public FakeTestCorSubmissionRepository Documents { get; } = new();
        public FakeTestParkingScheduleRepository Schedules { get; } = new();
        public FakeTestStudentRepository Students { get; } = new();
        public FakePersonnelRepository Personnel { get; } = new();
        public FakeAdminRepository Admins { get; } = new();
        public FakeViolationRepository Violations { get; } = new();
        public EntrySchedulePolicy SchedulePolicy { get; } = new();
        public Vehicle Vehicle { get; private set; } = null!;
        public CorSubmission Document { get; private set; } = null!;
        public Guid GuardUserId { get; private set; }

        public static async Task<Fixture> Create(bool canEnter = false)
        {
            var f = new Fixture();
            f.SchedulePolicy.CanEnterNow = canEnter;
            var owner = new UserAccount("hash", "+639123456789");
            var profile = new UserProfile(owner.Id, "Student", "One", null, "profile.jpg") { UserAccount = owner };
            profile.SubmitLicense("license.jpg"); profile.ReviewLicense(true, Guid.NewGuid(), "Approved");
            owner.UserProfile = profile;
            await f.Profiles.AddAsync(profile);
            await f.Students.AddAsync(new Student(profile.Id, "2023106763", "BSCS", "4A", 4) { UserProfile = profile });
            var guardProfile = new UserProfile(Guid.NewGuid(), "Guard", "One", null, null);
            await f.Profiles.AddAsync(guardProfile);
            f.GuardUserId = guardProfile.UserAccountId;
            f.Guards.Guard = new Guard(guardProfile, 1);
            f.Vehicle = new Vehicle(owner.Id, "ABC123", "Toyota", "QR-EARLY-STUDENT", VehicleType.Car,
                verificationStatus: CorVerificationStatus.Verified);
            typeof(Vehicle).GetProperty("Owner")!.SetValue(f.Vehicle, owner);
            await f.Vehicles.AddAsync(f.Vehicle);
            f.Document = new CorSubmission(owner.Id, "term", "doc.pdf", verificationStatus: CorVerificationStatus.Verified);
            await f.Documents.AddCorSubmissionAsync(f.Document);
            var today = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow).DayOfWeek;
            await f.Schedules.AddAsync(new ParkingSchedule(f.Document.Id, today, TimeSpan.Zero, new TimeSpan(23, 59, 59)));
            return f;
        }

        public CreateManualParkingLogHandler ManualEntry() => new(Logs, Vehicles, Profiles,
            new Test.Features.Auth.FakeUserAccountRepository(), Guards, Documents, Schedules, Students,
            Personnel, Admins, Violations, new ParkingService(), SchedulePolicy, new ParkingLogRoleService(),
            new FakeSignalRNotificationSender());
        public CreateManualParkingLogCommand Command() => new(Vehicle.PlateNumber, VehicleType.Car, null, Vehicle.Brand, GuardUserId);
    }

    private sealed class RecordingParkingLogRepository : FakeParkingLogRepository, ParkFlow.Application.Interfaces.IParkingLogRepository
    {
        public new Task<bool> CompleteExitAsync(ParkingLog log, IReadOnlyCollection<ParkingReservation> reservations, CancellationToken ct = default)
        { ActiveParkingLog = null; return Task.FromResult(true); }
        public new Task AddParkingLogAsync(ParkingLog log)
        {
            ActiveParkingLog = log;
            return Task.CompletedTask;
        }
    }

    private sealed class EntrySchedulePolicy : IScheduleService
    {
        public bool CanEnterNow { get; set; }
        // Isolate the early-entry branch from the machine clock and global early-buffer settings.
        public bool CanEnter(DateTime now, ParkingSchedule schedule, int? earlyBufferMinutes = null) => CanEnterNow;
        public TimeSpan GetEarliestAllowedEntryTime(ParkingSchedule schedule, int? earlyBufferMinutes = null) => TimeSpan.FromDays(1);
    }
}
