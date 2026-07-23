using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSessionCount;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSession;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace Test.Features.ParkingLogs;

public class GetSessionCountTests
{
    [Fact]
    public async Task Handle_ShouldIncludeManualSessionCount_WhenActiveLogsHaveManualEntryMethod()
    {
        // Arrange
        var ownerId1 = Guid.NewGuid();
        var ownerId2 = Guid.NewGuid();
        var vehicleId1 = Guid.NewGuid();
        var vehicleId2 = Guid.NewGuid();
        var guardId = Guid.NewGuid();

        var vehicle1 = new Vehicle(ownerId1, "ABC-1234", "Toyota", "qr1", VehicleType.Car);
        var vehicleIdProperty = typeof(BaseEntity).GetProperty("Id");
        vehicleIdProperty?.SetValue(vehicle1, vehicleId1);

        var vehicle2 = new Vehicle(ownerId2, "XYZ-5678", "Honda", "qr2", VehicleType.Car);
        vehicleIdProperty?.SetValue(vehicle2, vehicleId2);

        // Create active logs
        // Log 1: QrCode entry method
        var log1 = new ParkingLog(vehicleId1, guardId, ParkingStatus.Parked, EntryMethod.QrCode);
        var vehicleProperty = typeof(ParkingLog).GetProperty("Vehicle");
        vehicleProperty?.SetValue(log1, vehicle1);

        // Log 2: Manual entry method
        var log2 = new ParkingLog(vehicleId2, guardId, ParkingStatus.Parked, EntryMethod.Manual);
        vehicleProperty?.SetValue(log2, vehicle2);

        var activeLogs = new List<ParkingLog> { log1, log2 };

        var parkingLogRepository = new FakeParkingLogRepositoryWithActiveLogs(activeLogs);
        var corSubmissionRepository = new FakeCorSubmissionRepositoryWithMock(new List<CorSubmission>());
        var parkingScheduleRepository = new FakeParkingScheduleRepositoryWithMock(new List<ParkingSchedule>());

        var handler = new GetSessionCountHandler(
            parkingLogRepository,
            corSubmissionRepository,
            parkingScheduleRepository
        );

        var query = new GetSessionCountQuery(100);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.ActiveSessionCount);
        Assert.Equal(1, result.Data.ManualSessionCount);
        Assert.Equal(100, result.Data.MaximumCapacity);
    }

    [Fact]
    public async Task Handle_GetActiveSessions_ShouldIncludeEntryMethod()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var guardId = Guid.NewGuid();

        // Construct UserAccount and UserProfile
        var userAccount = new UserAccount(string.Empty, "+639999999999");
        var userProfile = new UserProfile(ownerId, "John", "Doe", null, null);
        userProfile.UserAccount = userAccount;
        userAccount.UserProfile = userProfile;

        // Construct Vehicle
        var vehicle = new Vehicle(ownerId, "ABC-1234", "Toyota", "qr1", VehicleType.Car);
        var vehicleIdProperty = typeof(BaseEntity).GetProperty("Id");
        vehicleIdProperty?.SetValue(vehicle, vehicleId);

        // Use reflection to set Vehicle.Owner
        var ownerProperty = typeof(Vehicle).GetProperty("Owner");
        ownerProperty?.SetValue(vehicle, userAccount);

        // Construct ParkingLog
        var log = new ParkingLog(vehicleId, guardId, ParkingStatus.Parked, EntryMethod.Manual);
        var vehicleProperty = typeof(ParkingLog).GetProperty("Vehicle");
        vehicleProperty?.SetValue(log, vehicle);

        var activeLogs = new List<ParkingLog> { log };

        var parkingLogRepository = new FakeParkingLogRepositoryWithActiveLogs(activeLogs);
        var corSubmissionRepository = new FakeCorSubmissionRepositoryWithMock(new List<CorSubmission>());
        var parkingScheduleRepository = new FakeParkingScheduleRepositoryWithMock(new List<ParkingSchedule>());
        var violationService = new FakeViolationService();
        var adminRepository = new FakeAdminRepository();
        var parkingLogRoleService = new ParkingLogRoleService();

        var handler = new GetActiveParkingSessionHandler(
            parkingLogRepository,
            parkingScheduleRepository,
            corSubmissionRepository,
            violationService,
            adminRepository,
            parkingLogRoleService
        );

        var query = new GetActiveParkingSessionQuery(100);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        var session = Assert.Single(result.Data);
        Assert.Equal("Manual", session.EntryMethod);
        Assert.Equal("ABC-1234", session.PlateNumber);
    }

    [Fact]
    public async Task Handle_SessionCount_ShouldNotIncludeManualLogsInOverstayCount_EvenIfScheduleEnded()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var guardId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var vehicle = new Vehicle(ownerId, "ABC-1234", "Toyota", "qr1", VehicleType.Car);
        var vehicleIdProperty = typeof(BaseEntity).GetProperty("Id");
        vehicleIdProperty?.SetValue(vehicle, vehicleId);

        // Manual log (should be skipped from overstay checks)
        var log = new ParkingLog(vehicleId, guardId, ParkingStatus.Parked, EntryMethod.Manual);
        var vehicleProperty = typeof(ParkingLog).GetProperty("Vehicle");
        vehicleProperty?.SetValue(log, vehicle);

        // Entry time is 5 hours ago
        var entryTimeProperty = typeof(ParkingLog).GetProperty("EntryTime");
        entryTimeProperty?.SetValue(log, DateTime.UtcNow.AddHours(-5));

        var activeLogs = new List<ParkingLog> { log };

        // Verified COR and schedule that ended 2 hours ago
        var cor = new CorSubmission(ownerId, "term-1", "doc-url");
        var corIdProperty = typeof(BaseEntity).GetProperty("Id");
        corIdProperty?.SetValue(cor, submissionId);
        cor.UpdateSubmission(null, null, CorVerificationStatus.Verified);

        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);
        var scheduleEndTime = philippinesNow.TimeOfDay.Subtract(TimeSpan.FromHours(2));
        if (scheduleEndTime < TimeSpan.Zero) scheduleEndTime = TimeSpan.FromHours(1);

        var schedule = new ParkingSchedule(submissionId, philippinesNow.DayOfWeek, TimeSpan.FromHours(6), scheduleEndTime);

        var parkingLogRepository = new FakeParkingLogRepositoryWithActiveLogs(activeLogs);
        var corSubmissionRepository = new FakeCorSubmissionRepositoryWithMock(new List<CorSubmission> { cor });
        var parkingScheduleRepository = new FakeParkingScheduleRepositoryWithMock(new List<ParkingSchedule> { schedule });

        var handler = new GetSessionCountHandler(
            parkingLogRepository,
            corSubmissionRepository,
            parkingScheduleRepository
        );

        var query = new GetSessionCountQuery(100);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.OverstayCount); // Should be 0 since entry method is manual
        Assert.Equal(1, result.Data.ManualSessionCount);
    }

    [Fact]
    public async Task Handle_GetActiveSessions_ShouldNotComputeOverstayForManualLogs_EvenIfScheduleEnded()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var guardId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        // Construct UserAccount and UserProfile
        var userAccount = new UserAccount(string.Empty, "+639999999999");
        var userProfile = new UserProfile(ownerId, "John", "Doe", null, null);
        userProfile.UserAccount = userAccount;
        userAccount.UserProfile = userProfile;

        var vehicle = new Vehicle(ownerId, "ABC-1234", "Toyota", "qr1", VehicleType.Car);
        var vehicleIdProperty = typeof(BaseEntity).GetProperty("Id");
        vehicleIdProperty?.SetValue(vehicle, vehicleId);

        // Use reflection to set Vehicle.Owner
        var ownerProperty = typeof(Vehicle).GetProperty("Owner");
        ownerProperty?.SetValue(vehicle, userAccount);

        // Manual log
        var log = new ParkingLog(vehicleId, guardId, ParkingStatus.Parked, EntryMethod.Manual);
        var vehicleProperty = typeof(ParkingLog).GetProperty("Vehicle");
        vehicleProperty?.SetValue(log, vehicle);

        // Entry time is 5 hours ago
        var entryTimeProperty = typeof(ParkingLog).GetProperty("EntryTime");
        entryTimeProperty?.SetValue(log, DateTime.UtcNow.AddHours(-5));

        var activeLogs = new List<ParkingLog> { log };

        // Verified COR and schedule that ended 2 hours ago
        var cor = new CorSubmission(ownerId, "term-1", "doc-url");
        var corIdProperty = typeof(BaseEntity).GetProperty("Id");
        corIdProperty?.SetValue(cor, submissionId);
        cor.UpdateSubmission(null, null, CorVerificationStatus.Verified);

        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);
        var scheduleEndTime = philippinesNow.TimeOfDay.Subtract(TimeSpan.FromHours(2));
        if (scheduleEndTime < TimeSpan.Zero) scheduleEndTime = TimeSpan.FromHours(1);

        var schedule = new ParkingSchedule(submissionId, philippinesNow.DayOfWeek, TimeSpan.FromHours(6), scheduleEndTime);

        var parkingLogRepository = new FakeParkingLogRepositoryWithActiveLogs(activeLogs);
        var corSubmissionRepository = new FakeCorSubmissionRepositoryWithMock(new List<CorSubmission> { cor });
        var parkingScheduleRepository = new FakeParkingScheduleRepositoryWithMock(new List<ParkingSchedule> { schedule });
        var violationService = new FakeViolationService();
        var adminRepository = new FakeAdminRepository();
        var parkingLogRoleService = new ParkingLogRoleService();

        var handler = new GetActiveParkingSessionHandler(
            parkingLogRepository,
            parkingScheduleRepository,
            corSubmissionRepository,
            violationService,
            adminRepository,
            parkingLogRoleService
        );

        var query = new GetActiveParkingSessionQuery(100);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        var session = Assert.Single(result.Data);
        Assert.Equal("Manual", session.EntryMethod);
        Assert.Equal(0, session.OverstayHours); // Should be 0 since entry method is manual
        Assert.Equal(0, session.Amount); // Should be 0 since entry method is manual
    }
}

public class FakeParkingLogRepositoryWithActiveLogs : IParkingLogRepository
{
    private readonly IReadOnlyList<ParkingLog> _activeLogs;

    public FakeParkingLogRepositoryWithActiveLogs(IReadOnlyList<ParkingLog> activeLogs)
    {
        _activeLogs = activeLogs;
    }

    public Task AddParkingLogAsync(ParkingLog parkingLog) => Task.CompletedTask;
    public Task<ParkingLog?> GetActiveParkingLogByVehicleIdAsync(Guid vehicleId) => Task.FromResult<ParkingLog?>(null);
    public Task<IReadOnlyList<ParkingLog>> GetActiveParkingLogsAsync(int limit) => Task.FromResult(_activeLogs);
    public Task<IReadOnlyList<ParkingLog>> GetTodaysParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetRecentParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetParkingHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task UpdateParkingLogAsync(ParkingLog parkingLog) => Task.CompletedTask;
}

public class FakeViolationService : IViolationService
{
    public bool IsOverstay(DateTime exitTime, TimeSpan scheduleEndTime, int graceMinutes = 30) => false;
    public TimeSpan GetOverstayDuration(DateTime exitTime, TimeSpan scheduleEndTime, int graceMinutes = 30) => TimeSpan.Zero;
    public decimal CalculatePenalty(TimeSpan overstayDuration, decimal hourlyRate = 5) => 0m;
}

public class FakeAdminRepository : IAdminRepository
{
    public Task AddAsync(Admin entity) => Task.CompletedTask;
    public Task DeleteAsync(Admin entity) => Task.CompletedTask;
    public Task<Admin?> GetByIdAsync(Guid id) => Task.FromResult<Admin?>(null);
    public Task<Admin?> GetByUserProfileIdAsync(Guid userProfileId) => Task.FromResult<Admin?>(null);
    public Task<IEnumerable<Admin>> ListAllAsync() => Task.FromResult<IEnumerable<Admin>>(new List<Admin>());
    public Task UpdateAsync(Admin entity) => Task.CompletedTask;
}
