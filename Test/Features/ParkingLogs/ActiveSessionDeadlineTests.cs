using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveSessionByVehicleId;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Xunit;

namespace Test.Features.ParkingLogs;

[CollectionDefinition("Parking deadline settings", DisableParallelization = true)]
public class ParkingDeadlineSettingsCollection { }

[Collection("Parking deadline settings")]
public class ActiveSessionDeadlineTests
{
    [Theory]
    [InlineData("student", true)]
    [InlineData("student", false)]
    [InlineData("reservation", true)]
    [InlineData("reservation", false)]
    [InlineData("completed", true)]
    [InlineData("special", true)]
    [InlineData("staff", true)]
    [InlineData("faculty", true)]
    [InlineData("manual", true)]
    [InlineData("manual-scheduled", true)]
    [InlineData("fallback", true)]
    [InlineData("overnight", true)]
    [InlineData("midnight-grace", true)]
    [InlineData("legacy-cor", true)]
    [InlineData("legacy-early-entry", true)]
    [InlineData("cancelled", true)]
    [InlineData("adjacent", true)]
    [InlineData("split-boundary", true)]
    [InlineData("legacy-outside-hours", true)]
    [InlineData("legacy-gap", true)]
    public async Task DetailsUseSharedPolicyAndCheckoutDeadline(string scenario, bool graceEnabled)
    {
        var settings = SystemSettingsStore.Current;
        var previousSettings = SystemSettingsStore.Current;
        // Existing settings default to 15; test configuration, not a new parking rule.
        settings.IsGracePeriodEnabled = graceEnabled;
        SystemSettingsStore.Replace(settings);
        try
        {
            var day = new DateTime(2026, 10, 8);
            var owner = new UserAccount("hash", "09171234567");
            var profile = new UserProfile(owner.Id, "Driver", "One", null, "photo") { UserAccount = owner };
            owner.UserProfile = profile;
            if (scenario is "staff" or "faculty")
                profile.Personnel = new Personnel(profile.Id, "EMP-1", "Office", scenario == "faculty" ? Roles.UniversityStaff : Roles.NonAcademicPersonnel);
            var vehicle = new Vehicle(owner.Id, "ABC123", "Toyota", "qr", VehicleType.Car);
            typeof(Vehicle).GetProperty("Owner")!.SetValue(vehicle, owner);
            var entryLocal = day.AddHours(scenario is "overnight" or "midnight-grace" ? 23 : 8);
            if (scenario is "legacy-early-entry" or "legacy-outside-hours") entryLocal = day.AddHours(7.5);
            if (scenario == "adjacent") entryLocal = day.AddHours(9);
            if (scenario == "split-boundary") entryLocal = day.AddHours(10);
            if (scenario == "legacy-gap") entryLocal = day.AddHours(11);
            var entry = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entryLocal, entryLocal.TimeOfDay);
            var now = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(day.AddDays(1), TimeSpan.FromHours(8));
            var method = scenario == "manual" ? EntryMethod.Manual : scenario == "manual-scheduled" ? EntryMethod.ManualScheduled : EntryMethod.QrCode;
            var log = new ParkingLog(vehicle.Id, null, ParkingStatus.Parked, method);
            typeof(ParkingLog).GetProperty("EntryTime")!.SetValue(log, entry);
            // The real single-session repository returns an untracked log without navigation properties.
            var vehicles = new Test.Support.FakeVehicleRepository(); await vehicles.AddAsync(vehicle);
            var document = new CorSubmission(owner.Id, "term", "cor", verificationStatus: scenario == "legacy-cor" ? CorVerificationStatus.Pending : CorVerificationStatus.Verified);
            var hasSchedule = scenario is "student" or "legacy-cor" or "manual-scheduled" or "midnight-grace" or "adjacent" or "split-boundary" or "legacy-outside-hours" or "legacy-gap";
            var schedule = new ParkingSchedule(document.Id, day.DayOfWeek, TimeSpan.FromHours(8),
                scenario == "midnight-grace" ? new TimeSpan(23, 55, 0) : TimeSpan.FromHours(10));
            var bookings = new FakeTestParkingReservationRepository();
            ParkingReservation? booking = null;
            if (scenario is "reservation" or "completed" or "special" or "legacy-early-entry" or "cancelled")
            {
                booking = new ParkingReservation(owner.Id, "RES-1", day, TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Visit",
                    type: scenario == "special" ? ReservationType.Special : ReservationType.Normal, vehicleId: vehicle.Id);
                booking.Approve(Guid.NewGuid());
                if (scenario == "completed") booking.MarkCompleted();
                if (scenario == "cancelled") booking.Cancel();
                await bookings.AddAsync(booking);
            }
            var documents = new[] { document };
            var schedules = hasSchedule ? new[] { schedule } : [];
            if (scenario is "adjacent" or "split-boundary")
                schedules = [schedule, new ParkingSchedule(document.Id, day.DayOfWeek, TimeSpan.FromHours(10), TimeSpan.FromHours(12))];
            if (scenario is "legacy-outside-hours" or "legacy-gap")
                schedules = [schedule, new ParkingSchedule(document.Id, day.DayOfWeek, TimeSpan.FromHours(13), TimeSpan.FromHours(17))];
            var result = await new GetActiveSessionByVehicleIdHandler(new FakeParkingLogRepositoryForExit(log), vehicles,
                new FakeParkingScheduleRepositoryWithMock(schedules), new FakeCorSubmissionRepositoryWithMock(documents), bookings,
                timeProvider: new FixedClock(now)).Handle(new(vehicle.Id), default);
            Assert.True(result.IsSuccess, result.Message);
            var shared = ActiveSessionTiming.ResolveActive(log, now, settings, documents, schedules.ToLookup(s => s.SubmissionId),
                await bookings.GetByUserIdAsync(owner.Id), profile.Personnel, vehicle);
            Assert.Equal(shared.MaximumExitTime?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "N/A", result.Data!.ExitBy);
            Assert.Equal(shared.OverstayHours, result.Data.OverstayHours);
            if (scenario == "student") Assert.Equal(day.AddHours(10).AddMinutes(graceEnabled ? settings.GracePeriodMinutes : 0).AddHours(-8), shared.MaximumExitTime);
            if (scenario == "reservation") Assert.Equal(day.AddHours(9).AddMinutes(graceEnabled ? settings.GracePeriodMinutes : 0).AddHours(-8), shared.MaximumExitTime);
            var eligibleBooking = scenario is "reservation" or "completed" or "special" ? booking : null;
            var checkoutSchedule = document.VerificationStatus == CorVerificationStatus.Verified
                ? ScheduleService.FindEntrySchedule(schedules, entryLocal) : null;
            var checkout = ActiveSessionTiming.ResolveCheckout(entry, method, profile.Personnel, eligibleBooking, checkoutSchedule, settings, now);
            if (scenario is "adjacent" or "split-boundary") Assert.Equal(day.AddHours(12).AddMinutes(settings.GracePeriodMinutes).AddHours(-8), shared.MaximumExitTime);
            if (scenario is "legacy-outside-hours" or "legacy-gap") Assert.Equal(day.AddHours(10).AddMinutes(settings.GracePeriodMinutes).AddHours(-8), shared.MaximumExitTime);
            if (scenario == "legacy-cor") Assert.Equal(day.AddHours(22).AddHours(-8), shared.MaximumExitTime);
            Assert.Equal(checkout.MaximumExitTime, shared.MaximumExitTime);
            Assert.Equal(checkout.OverstayHours, shared.OverstayHours, 6);
            Assert.Equal(entry.ToString("yyyy-MM-ddTHH:mm:ssZ"), result.Data.StartedAt);
            // List/count consumers load navigations; the detail endpoint above does not.
            typeof(ParkingLog).GetProperty("Vehicle")!.SetValue(log, vehicle);
            var activeLogs = new FakeParkingLogRepositoryWithActiveLogs([log]);
            var documentsRepo = new FakeCorSubmissionRepositoryWithMock(documents);
            var schedulesRepo = new FakeParkingScheduleRepositoryWithMock(schedules);
            var listed = await new ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSession.GetActiveParkingSessionHandler(
                activeLogs, schedulesRepo, documentsRepo, new FakeAdminRepository(), new ParkingLogRoleService(), bookings)
                .Handle(new(500), default);
            var counted = await new ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSessionCount.GetSessionCountHandler(
                activeLogs, documentsRepo, schedulesRepo, bookings).Handle(new(500), default);
            Assert.True(listed.IsSuccess);
            Assert.True(counted.IsSuccess);
            var listedSession = Assert.Single(listed.Data!);
            Assert.Equal(shared.MaximumExitTime, listedSession.MaximumExitTime);
            Assert.Equal(listedSession.Status == "Overstay" ? 1 : 0, counted.Data!.OverstayCount);
            Assert.Equal(1, counted.Data.ActiveSessionCount);
        }
        finally { SystemSettingsStore.Replace(previousSettings); }
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }
}
