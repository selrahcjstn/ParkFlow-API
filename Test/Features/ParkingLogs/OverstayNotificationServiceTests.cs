using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Infrastructure.Services;
using Xunit;

namespace Test.Features.ParkingLogs;

public class OverstayNotificationServiceTests
{
    private static readonly DateTime BaseDay = new(2026, 11, 3);
    private static DateTime Utc(int hour, int minute = 0, int dayOffset = 0) =>
        ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(BaseDay.AddDays(dayOffset), new TimeSpan(hour, minute, 0));

    private static (ParkingLog Log, UserAccount Owner, Vehicle Vehicle) CreateParkedSession(
        DateTime entryUtc,
        string plateNumber = "NTH123",
        string email = "student@bulsu.edu.ph",
        Roles? role = null)
    {
        var owner = new UserAccount("hash", "+639123456789");
        owner.AuthIdentities.Add(AuthIdentity.CreateManual(owner.Id, email, "hash", isPrimary: true));
        var profile = new UserProfile(owner.Id, "Juan", "Dela Cruz", null, null) { UserAccount = owner };
        owner.UserProfile = profile;

        if (role.HasValue)
        {
            profile.Personnel = new Personnel(profile.Id, "EMP-100", "Admin Office", role.Value);
        }

        var vehicle = new Vehicle(owner.Id, plateNumber, "Toyota Vios", "VEH-HASH", VehicleType.Car);
        typeof(Vehicle).GetProperty("Owner")!.SetValue(vehicle, owner);

        var log = new ParkingLog(vehicle.Id, null, ParkingStatus.Parked, EntryMethod.QrCode);
        typeof(ParkingLog).GetProperty("EntryTime")!.SetValue(log, entryUtc);
        typeof(ParkingLog).GetProperty("Vehicle")!.SetValue(log, vehicle);

        return (log, owner, vehicle);
    }

    private class FakeParkingLogRepo : IParkingLogRepository
    {
        public List<ParkingLog> ActiveLogs { get; } = new();
        public Task<IReadOnlyList<ParkingLog>> GetActiveParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(ActiveLogs);
        public Task<bool> CompleteExitAsync(ParkingLog log, IReadOnlyCollection<ParkingReservation> reservations, CancellationToken ct = default) => Task.FromResult(true);
        public Task AddParkingLogAsync(ParkingLog parkingLog) => Task.CompletedTask;
        public Task<ParkingLog?> GetActiveParkingLogByVehicleIdAsync(Guid vehicleId) => Task.FromResult<ParkingLog?>(null);
        public Task<bool> HasActiveParkingLogByUserIdAsync(Guid userId) => Task.FromResult(false);
        public Task<IReadOnlyList<ParkingLog>> GetTodaysParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
        public Task<IReadOnlyList<ParkingLog>> GetRecentParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
        public Task<IReadOnlyList<ParkingLog>> GetParkingHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
        public Task UpdateParkingLogAsync(ParkingLog parkingLog) => Task.CompletedTask;
    }

    private class FakeCorRepo : ICorSubmissionRepository
    {
        public List<CorSubmission> Submissions { get; } = new();
        public Task<IEnumerable<CorSubmission>> ListCorSubmissionsAsync() => Task.FromResult<IEnumerable<CorSubmission>>(Submissions);
        public Task<CorSubmission?> GetCorSubmissionAsync(Guid id) => Task.FromResult(Submissions.FirstOrDefault(s => s.Id == id));
        public Task<CorSubmission?> GetByUserIdAndTermAsync(Guid userAccountId, string academicTerm) => Task.FromResult<CorSubmission?>(null);
        public Task<CorSubmission?> GetLatestByUserIdAsync(Guid userAccountId) => Task.FromResult(Submissions.LastOrDefault(s => s.UserAccountId == userAccountId));
        public Task AddCorSubmissionAsync(CorSubmission corSubmission) { Submissions.Add(corSubmission); return Task.CompletedTask; }
        public Task UpdateCorSubmissionAsync(CorSubmission corSubmission) => Task.CompletedTask;
        public Task DeleteCorSubmissionAsync(CorSubmission corSubmission) => Task.CompletedTask;
    }

    private class FakeScheduleRepo : IParkingScheduleRepository
    {
        public List<ParkingSchedule> Schedules { get; } = new();
        public Task<IEnumerable<ParkingSchedule>> GetBySubmissionIdAsync(Guid submissionId) =>
            Task.FromResult<IEnumerable<ParkingSchedule>>(Schedules.Where(s => s.SubmissionId == submissionId).ToList());
        public Task AddAsync(ParkingSchedule parkingSchedule) { Schedules.Add(parkingSchedule); return Task.CompletedTask; }
        public Task<ParkingSchedule?> GetByIdAsync(Guid id) => Task.FromResult(Schedules.FirstOrDefault(s => s.Id == id));
        public Task<IEnumerable<ParkingSchedule>> GetByUserIdAsync(Guid userId) => Task.FromResult<IEnumerable<ParkingSchedule>>(new List<ParkingSchedule>());
        public Task UpdateAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
        public Task DeleteAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
        public Task ReplaceSchedulesAsync(Guid submissionId, IEnumerable<ParkingSchedule> newSchedules) => Task.CompletedTask;
    }

    private class FakeUserNotifRepo : IUserNotificationRepository
    {
        public HashSet<(Guid UserId, string RefCode)> ExistingReferenceCodes { get; } = new();
        public Task<bool> ExistsByReferenceCodeAsync(Guid userId, string referenceCode) =>
            Task.FromResult(ExistingReferenceCodes.Contains((userId, referenceCode)));
        public Task AddAsync(UserNotification notification) => Task.CompletedTask;
        public Task<UserNotification?> GetByIdAsync(Guid id) => Task.FromResult<UserNotification?>(null);
        public Task<IEnumerable<UserNotification>> GetByUserIdAsync(Guid userId, int limit = 20, int skip = 0) => Task.FromResult<IEnumerable<UserNotification>>(new List<UserNotification>());
        public Task<int> GetUnreadCountAsync(Guid userId) => Task.FromResult(0);
        public Task UpdateAsync(UserNotification notification) => Task.CompletedTask;
        public Task MarkAllAsReadForUserAsync(Guid userId) => Task.CompletedTask;
        public Task DeleteAsync(UserNotification notification) => Task.CompletedTask;
    }

    public record SentNotification(
        Guid UserAccountId,
        string Title,
        string Body,
        string Type,
        string? Subtitle,
        string? ReferenceCode,
        string? VehiclePlate,
        string? ActionRoute,
        string? ActionText,
        string? Priority,
        string? Issuer,
        string? DriverName,
        string? DriverRole,
        string? VehicleBrand,
        object? SignalRData);

    private class FakeNotificationService : INotificationService
    {
        public List<SentNotification> Sent { get; } = new();
        public Task CreateAndSendNotificationAsync(
            Guid userAccountId,
            string title,
            string body,
            string type = "system",
            string? subtitle = null,
            string? referenceCode = null,
            string? vehiclePlate = null,
            string? actionRoute = null,
            string? actionText = null,
            string? priority = null,
            string? issuer = null,
            string? driverName = null,
            string? driverRole = null,
            string? vehicleBrand = null,
            object? signalRData = null)
        {
            Sent.Add(new SentNotification(
                userAccountId, title, body, type, subtitle, referenceCode,
                vehiclePlate, actionRoute, actionText, priority, issuer,
                driverName, driverRole, vehicleBrand, signalRData));
            return Task.CompletedTask;
        }
    }

    public record SentEmail(string To, string Subject, string HtmlBody);

    private class FakeEmailService : IEmailService
    {
        public List<SentEmail> Sent { get; } = new();
        public Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            Sent.Add(new SentEmail(to, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task WhenSessionOverstays_SendsInitialInAppAlertAndSingleEmail()
    {
        OverstayNotificationService.ClearInMemoryCacheForTesting();

        // Entry at 8:00 AM Philippines time. Schedule deadline at 10:00 AM
        var entry = Utc(8);
        var (log, owner, _) = CreateParkedSession(entry, "ABC1234", "user@bulsu.edu.ph");

        var parkingLogRepo = new FakeParkingLogRepo();
        parkingLogRepo.ActiveLogs.Add(log);

        var submissionId = Guid.NewGuid();
        var cor = new CorSubmission(owner.Id, "url", "hash", "1st Sem", "2026-2027", CorVerificationStatus.Verified);
        typeof(CorSubmission).GetProperty("Id")!.SetValue(cor, submissionId);
        var corRepo = new FakeCorRepo();
        corRepo.Submissions.Add(cor);

        var schedule = new ParkingSchedule(submissionId, DayOfWeek.Tuesday, new TimeSpan(8, 0, 0), new TimeSpan(10, 0, 0));
        var scheduleRepo = new FakeScheduleRepo();
        scheduleRepo.Schedules.Add(schedule);

        var notificationService = new FakeNotificationService();
        var userNotifRepo = new FakeUserNotifRepo();
        var emailService = new FakeEmailService();

        var service = new OverstayNotificationService(
            parkingLogRepo,
            corRepo,
            scheduleRepo,
            notificationService,
            userNotifRepo,
            NullLogger<OverstayNotificationService>.Instance,
            emailService: emailService
        );

        // Check at 10:20 AM (20 minutes past deadline -> Overstaying, < 1 hr)
        var checkTime = Utc(10, 20);
        var count = await service.CheckAndSendOverstayNotificationsAsync(checkTime, CancellationToken.None);

        Assert.Equal(1, count);

        // Verify In-App Notification was sent with type "overdue" and correct reference code
        Assert.Single(notificationService.Sent);
        var notif = notificationService.Sent[0];
        Assert.Equal(owner.Id, notif.UserAccountId);
        Assert.Equal("Parking Overstay Alert", notif.Title);
        Assert.Contains("ABC1234", notif.Body);
        Assert.Equal("overdue", notif.Type);
        Assert.Equal($"OVERSTAY-INITIAL-{log.Id}", notif.ReferenceCode);
        Assert.Equal("ABC1234", notif.VehiclePlate);
        Assert.Equal("(tabs)/home", notif.ActionRoute);
        Assert.Equal("high", notif.Priority);

        // Verify Email was sent once
        Assert.Single(emailService.Sent);
        var email = emailService.Sent[0];
        Assert.Equal("user@bulsu.edu.ph", email.To);
        Assert.Contains("ABC1234", email.Subject);
        Assert.Contains("Overstay", email.Subject);
        Assert.Contains("ABC1234", email.HtmlBody);
        Assert.Contains("Parking Overstay Notice", email.HtmlBody);
    }

    [Fact]
    public async Task WhenSessionAlreadyNotified_DoesNotDuplicateInitialAlertOrEmail()
    {
        OverstayNotificationService.ClearInMemoryCacheForTesting();

        var entry = Utc(8);
        var (log, owner, _) = CreateParkedSession(entry, "ABC1234", "user@bulsu.edu.ph");

        var parkingLogRepo = new FakeParkingLogRepo();
        parkingLogRepo.ActiveLogs.Add(log);

        var submissionId = Guid.NewGuid();
        var cor = new CorSubmission(owner.Id, "url", "hash", "1st Sem", "2026-2027", CorVerificationStatus.Verified);
        typeof(CorSubmission).GetProperty("Id")!.SetValue(cor, submissionId);
        var corRepo = new FakeCorRepo();
        corRepo.Submissions.Add(cor);

        var schedule = new ParkingSchedule(submissionId, DayOfWeek.Tuesday, new TimeSpan(8, 0, 0), new TimeSpan(10, 0, 0));
        var scheduleRepo = new FakeScheduleRepo();
        scheduleRepo.Schedules.Add(schedule);

        var notificationService = new FakeNotificationService();
        var userNotifRepo = new FakeUserNotifRepo();
        // Record that DB already has the initial notification
        userNotifRepo.ExistingReferenceCodes.Add((owner.Id, $"OVERSTAY-INITIAL-{log.Id}"));

        var emailService = new FakeEmailService();

        var service = new OverstayNotificationService(
            parkingLogRepo,
            corRepo,
            scheduleRepo,
            notificationService,
            userNotifRepo,
            NullLogger<OverstayNotificationService>.Instance,
            emailService: emailService
        );

        // Check at 10:20 AM (still within first hour of overstay)
        var checkTime = Utc(10, 20);
        var count = await service.CheckAndSendOverstayNotificationsAsync(checkTime, CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Empty(notificationService.Sent);
        Assert.Empty(emailService.Sent);
    }

    [Fact]
    public async Task WhenOverstayReachesOneHour_SendsHourlyReminderInAppOnly_NoEmail()
    {
        OverstayNotificationService.ClearInMemoryCacheForTesting();

        var entry = Utc(8);
        var (log, owner, _) = CreateParkedSession(entry, "XYZ8888", "driver@bulsu.edu.ph");

        var parkingLogRepo = new FakeParkingLogRepo();
        parkingLogRepo.ActiveLogs.Add(log);

        var submissionId = Guid.NewGuid();
        var cor = new CorSubmission(owner.Id, "url", "hash", "1st Sem", "2026-2027", CorVerificationStatus.Verified);
        typeof(CorSubmission).GetProperty("Id")!.SetValue(cor, submissionId);
        var corRepo = new FakeCorRepo();
        corRepo.Submissions.Add(cor);

        var schedule = new ParkingSchedule(submissionId, DayOfWeek.Tuesday, new TimeSpan(8, 0, 0), new TimeSpan(10, 0, 0));
        var scheduleRepo = new FakeScheduleRepo();
        scheduleRepo.Schedules.Add(schedule);

        var notificationService = new FakeNotificationService();
        var userNotifRepo = new FakeUserNotifRepo();

        // Initial alert was already processed in DB
        userNotifRepo.ExistingReferenceCodes.Add((owner.Id, $"OVERSTAY-INITIAL-{log.Id}"));

        var emailService = new FakeEmailService();

        var service = new OverstayNotificationService(
            parkingLogRepo,
            corRepo,
            scheduleRepo,
            notificationService,
            userNotifRepo,
            NullLogger<OverstayNotificationService>.Instance,
            emailService: emailService
        );

        // Check at 11:15 AM (1 hour 15 minutes past 10:00 AM deadline)
        var checkTime = Utc(11, 15);
        var count = await service.CheckAndSendOverstayNotificationsAsync(checkTime, CancellationToken.None);

        Assert.Equal(1, count);

        // Verify In-App Hourly Reminder was sent with type "reminder" and subtitle "1 Hour Overstay"
        Assert.Single(notificationService.Sent);
        var notif = notificationService.Sent[0];
        Assert.Equal(owner.Id, notif.UserAccountId);
        Assert.Equal("Parking Overstay Reminder", notif.Title);
        Assert.Contains("XYZ8888", notif.Body);
        Assert.Contains("1 hour", notif.Body);
        Assert.Equal("reminder", notif.Type);
        Assert.Equal("1 Hour Overstay", notif.Subtitle);
        Assert.Equal($"OVERSTAY-REMINDER-{log.Id}-H1", notif.ReferenceCode);

        // CRITICAL: Verify ZERO email was sent for the hourly reminder!
        Assert.Empty(emailService.Sent);
    }

    [Fact]
    public async Task WhenOverstayReachesMultipleHours_SendsEachHourReminderSequentially()
    {
        OverstayNotificationService.ClearInMemoryCacheForTesting();

        var entry = Utc(8);
        var (log, owner, _) = CreateParkedSession(entry, "XYZ8888", "driver@bulsu.edu.ph");

        var parkingLogRepo = new FakeParkingLogRepo();
        parkingLogRepo.ActiveLogs.Add(log);

        var submissionId = Guid.NewGuid();
        var cor = new CorSubmission(owner.Id, "url", "hash", "1st Sem", "2026-2027", CorVerificationStatus.Verified);
        typeof(CorSubmission).GetProperty("Id")!.SetValue(cor, submissionId);
        var corRepo = new FakeCorRepo();
        corRepo.Submissions.Add(cor);

        var schedule = new ParkingSchedule(submissionId, DayOfWeek.Tuesday, new TimeSpan(8, 0, 0), new TimeSpan(10, 0, 0));
        var scheduleRepo = new FakeScheduleRepo();
        scheduleRepo.Schedules.Add(schedule);

        var notificationService = new FakeNotificationService();
        var userNotifRepo = new FakeUserNotifRepo();

        // Initial alert was already processed, Hour 1 was already processed, Hour 2 is new
        userNotifRepo.ExistingReferenceCodes.Add((owner.Id, $"OVERSTAY-INITIAL-{log.Id}"));
        userNotifRepo.ExistingReferenceCodes.Add((owner.Id, $"OVERSTAY-REMINDER-{log.Id}-H1"));

        var emailService = new FakeEmailService();

        var service = new OverstayNotificationService(
            parkingLogRepo,
            corRepo,
            scheduleRepo,
            notificationService,
            userNotifRepo,
            NullLogger<OverstayNotificationService>.Instance,
            emailService: emailService
        );

        // Check at 12:15 PM (2 hours 15 minutes past 10:00 AM deadline)
        var checkTime = Utc(12, 15);
        var count = await service.CheckAndSendOverstayNotificationsAsync(checkTime, CancellationToken.None);

        Assert.Equal(1, count);

        // Verify In-App Hourly Reminder for Hour 2 was sent
        Assert.Single(notificationService.Sent);
        var notif = notificationService.Sent[0];
        Assert.Equal(owner.Id, notif.UserAccountId);
        Assert.Equal("Parking Overstay Reminder", notif.Title);
        Assert.Contains("XYZ8888", notif.Body);
        Assert.Contains("2 hours", notif.Body);
        Assert.Equal("reminder", notif.Type);
        Assert.Equal("2 Hours Overstay", notif.Subtitle);
        Assert.Equal($"OVERSTAY-REMINDER-{log.Id}-H2", notif.ReferenceCode);

        // CRITICAL: Still NO emails for hourly reminders
        Assert.Empty(emailService.Sent);
    }

    [Fact]
    public async Task WhenFaculty_IsExemptFromOverstayAndReceivesNoNotifications()
    {
        OverstayNotificationService.ClearInMemoryCacheForTesting();

        var entry = Utc(8);
        var (log, owner, _) = CreateParkedSession(entry, "FAC123", "faculty@bulsu.edu.ph", role: Roles.UniversityStaff);

        var parkingLogRepo = new FakeParkingLogRepo();
        parkingLogRepo.ActiveLogs.Add(log);

        var corRepo = new FakeCorRepo();
        var scheduleRepo = new FakeScheduleRepo();
        var notificationService = new FakeNotificationService();
        var userNotifRepo = new FakeUserNotifRepo();
        var emailService = new FakeEmailService();

        var service = new OverstayNotificationService(
            parkingLogRepo,
            corRepo,
            scheduleRepo,
            notificationService,
            userNotifRepo,
            NullLogger<OverstayNotificationService>.Instance,
            emailService: emailService
        );

        // Check at 11:00 PM
        var checkTime = Utc(23);
        var count = await service.CheckAndSendOverstayNotificationsAsync(checkTime, CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Empty(notificationService.Sent);
        Assert.Empty(emailService.Sent);
    }
}
