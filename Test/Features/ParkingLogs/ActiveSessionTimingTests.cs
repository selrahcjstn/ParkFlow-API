using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSession;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSessionCount;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Xunit;

namespace Test.Features.ParkingLogs;

public class ActiveSessionTimingTests
{
    private static readonly DateTime Day = new(2026, 10, 8);
    private static DateTime Utc(int hour, int minute = 0, int dayOffset = 0) =>
        ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(Day.AddDays(dayOffset), new TimeSpan(hour, minute, 0));

    private static ParkingLog Log(DateTime entry, EntryMethod method = EntryMethod.QrCode, Roles? role = null)
    {
        var owner = new UserAccount("hash", "+639123456789");
        var profile = new UserProfile(owner.Id, "Test", "Driver", null, null) { UserAccount = owner };
        owner.UserProfile = profile;
        if (role.HasValue) profile.Personnel = new Personnel(profile.Id, "EMP-1", "Office", role.Value);
        var vehicle = new Vehicle(owner.Id, "TEST123", "Toyota", "TEST-HASH", VehicleType.Car);
        typeof(Vehicle).GetProperty("Owner")!.SetValue(vehicle, owner);
        var log = new ParkingLog(vehicle.Id, null, ParkingStatus.Parked, method);
        typeof(ParkingLog).GetProperty("EntryTime")!.SetValue(log, entry);
        typeof(ParkingLog).GetProperty("Vehicle")!.SetValue(log, vehicle);
        return log;
    }

    private static (DateTime DeadlineUtc, bool HasReservation) Resolve(ParkingLog log, DateTime now,
        SystemSettingsDto? settings = null, CorSubmission[]? documents = null,
        ParkingSchedule[]? schedules = null, ParkingReservation[]? reservations = null) =>
        ActiveSessionTiming.Resolve(log, now, settings ?? new SystemSettingsDto(), documents ?? [],
            (schedules ?? []).ToLookup(s => s.SubmissionId), reservations ?? []);

    [Theory]
    [InlineData(Roles.UniversityStaff, EntryMethod.QrCode)]
    [InlineData(Roles.UniversityStaff, EntryMethod.ManualScheduled)]
    [InlineData(Roles.UniversityStaff, EntryMethod.Manual)]
    [InlineData(Roles.NonAcademicPersonnel, EntryMethod.QrCode)]
    [InlineData(Roles.NonAcademicPersonnel, EntryMethod.ManualScheduled)]
    [InlineData(Roles.NonAcademicPersonnel, EntryMethod.Manual)]
    public void PersonnelUsesConfiguredCutoffRatherThanDefaultTenPmOrManualMidnight(Roles role, EntryMethod method)
    {
        var log = Log(Utc(8), method, role);
        var settings = new SystemSettingsDto { PersonnelFreeParkingEnd = "18:00" };
        var timing = Resolve(log, Utc(19), settings);
        Assert.Equal(Utc(18), timing.DeadlineUtc);
        Assert.Equal(1, ActiveSessionTiming.GetOverstayHours(log, Utc(19), settings, timing.DeadlineUtc));
        Assert.Equal(16, ActiveSessionTiming.GetOverstayHours(log, Utc(10, dayOffset: 1), settings, timing.DeadlineUtc));
    }

    [Fact]
    public void PersonnelEarlyChargesAreStillShownDuringTheFreeWindow()
    {
        var log = Log(Utc(4), role: Roles.UniversityStaff);
        var settings = new SystemSettingsDto();
        var timing = Resolve(log, Utc(10), settings);
        Assert.Equal(1, ActiveSessionTiming.GetOverstayHours(log, Utc(10), settings, timing.DeadlineUtc));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScheduleSelectionMatchesEntryTimeAndSupportsExistingDocumentFallback(bool verified)
    {
        var log = Log(Utc(8));
        var document = new CorSubmission(log.Vehicle.OwnerId, "term-1", "doc-url");
        if (verified) document.UpdateSubmission(null, null, CorVerificationStatus.Verified);
        // Repository order is not the applicable schedule order.
        ParkingSchedule[] schedules = [
            new(document.Id, Day.DayOfWeek, TimeSpan.FromHours(13), TimeSpan.FromHours(17)),
            new(document.Id, Day.DayOfWeek, TimeSpan.FromHours(7), TimeSpan.FromHours(9))
        ];
        var settings = new SystemSettingsDto();
        var timing = Resolve(log, Utc(10), settings, [document], schedules);
        Assert.Equal(Utc(9, 15), timing.DeadlineUtc);
        Assert.Equal(0.75, ActiveSessionTiming.GetOverstayHours(log, Utc(10), settings, timing.DeadlineUtc));
    }

    [Theory]
    [InlineData(ReservationStatus.Approved, 9)]
    [InlineData(ReservationStatus.Completed, 9)]
    [InlineData(ReservationStatus.Pending, 9)]
    [InlineData(ReservationStatus.Rejected, 22)]
    [InlineData(ReservationStatus.Cancelled, 22)]
    public void ReservationSelectionPreservesSessionListStatusRules(ReservationStatus status, int endHour)
    {
        var log = Log(Utc(8));
        var reservation = new ParkingReservation(log.Vehicle.OwnerId, "RES-1", Day,
            TimeSpan.FromHours(7), TimeSpan.FromHours(9), "Campus visit", vehicleId: log.VehicleId);
        if (status == ReservationStatus.Approved) reservation.Approve(Guid.NewGuid());
        if (status == ReservationStatus.Completed) reservation.MarkCompleted();
        if (status == ReservationStatus.Rejected) reservation.Reject(Guid.NewGuid());
        if (status == ReservationStatus.Cancelled) reservation.Cancel();
        var settings = new SystemSettingsDto { IsGracePeriodEnabled = false };
        var timing = Resolve(log, Utc(10), settings, reservations: [reservation]);
        Assert.Equal(Utc(endHour), timing.DeadlineUtc);
        Assert.Equal(endHour == 9, timing.HasReservation);
        Assert.Equal(endHour == 9 ? 1 : 0, ActiveSessionTiming.GetOverstayHours(log, Utc(10), settings, timing.DeadlineUtc));
    }

    [Fact]
    public void ManualVisitorPassUsesEntryDayNotStudentScheduleAndExpiresOnTheNextDay()
    {
        var log = Log(Utc(8), EntryMethod.Manual);
        var settings = new SystemSettingsDto();
        var timing = Resolve(log, Utc(22), settings);
        Assert.Equal(Utc(0, dayOffset: 1).AddSeconds(-1), timing.DeadlineUtc);
        Assert.Equal(0, ActiveSessionTiming.GetOverstayHours(log, timing.DeadlineUtc.AddMilliseconds(500), settings, timing.DeadlineUtc));
        Assert.True(ActiveSessionTiming.GetOverstayHours(log, Utc(8, dayOffset: 1), settings, timing.DeadlineUtc) > 0);
    }

    [Fact]
    public async Task CountAndSessionListAgreeForPersonnelSchedulesReservationsAndManualSessions()
    {
        var yesterday = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow).Date.AddDays(-1);
        var entry = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(yesterday, TimeSpan.FromHours(8));
        ParkingLog[] logs = [Log(entry, role: Roles.UniversityStaff), Log(entry, EntryMethod.ManualScheduled, Roles.NonAcademicPersonnel),
            Log(entry), Log(entry), Log(entry, EntryMethod.Manual)];
        var document = new CorSubmission(logs[2].Vehicle.OwnerId, "term-1", "doc-url");
        var schedule = new ParkingSchedule(document.Id, yesterday.DayOfWeek, TimeSpan.FromHours(7), TimeSpan.FromHours(9));
        var reservation = new ParkingReservation(logs[3].Vehicle.OwnerId, "RES-1", yesterday,
            TimeSpan.FromHours(7), TimeSpan.FromHours(9), "Campus visit", vehicleId: logs[3].VehicleId);
        reservation.MarkCompleted();
        var reservations = new FakeTestParkingReservationRepository();
        await reservations.AddAsync(reservation);
        var repository = new FakeParkingLogRepositoryWithActiveLogs(logs);
        var documents = new FakeCorSubmissionRepositoryWithMock([document]);
        var schedules = new FakeParkingScheduleRepositoryWithMock([schedule]);
        var counts = await new GetSessionCountHandler(repository, documents, schedules, reservations)
            .Handle(new GetSessionCountQuery(500), default);
        var sessions = await new GetActiveParkingSessionHandler(repository, schedules, documents,
            new FakeViolationService(), new FakeAdminRepository(), new ParkingLogRoleService(), reservations)
            .Handle(new GetActiveParkingSessionQuery(500), default);
        Assert.True(counts.IsSuccess);
        Assert.True(sessions.IsSuccess);
        Assert.Equal(5, counts.Data!.OverstayCount);
        Assert.Equal(sessions.Data!.Count(s => s.Status == "Overstay"), counts.Data.OverstayCount);
        Assert.Equal(5, counts.Data.ActiveSessionCount);
    }
}
