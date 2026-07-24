using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.ExitManualParkingLog;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Auth;
using Test.Features.Violations;

namespace Test.Features.ParkingLogs;

public class ExitManualParkingLogTests
{
    private readonly FakeVehicleRepository _vehicleRepository;
    private readonly FakeUserProfileRepository _userProfileRepository;
    private readonly FakeUserAccountRepository _userAccountRepository;
    private readonly FakeGuardRepository _guardRepository;
    private readonly FakeCorSubmissionRepository _corSubmissionRepository;
    private readonly FakeParkingScheduleRepository _parkingScheduleRepository;
    private readonly FakeStudentRepository _studentRepository;
    private readonly FakePersonnelRepository _personnelRepository;
    private readonly FakeAdminRepository _adminRepository;
    private readonly FakeViolationRepository _violationRepository;
    private readonly IParkingService _parkingService;
    private readonly FakeScheduleService _scheduleService;
    private readonly ParkingLogRoleService _parkingLogRoleService;

    public ExitManualParkingLogTests()
    {
        _vehicleRepository = new FakeVehicleRepository();
        _userProfileRepository = new FakeUserProfileRepository();
        _userAccountRepository = new FakeUserAccountRepository();
        _guardRepository = new FakeGuardRepository();
        _corSubmissionRepository = new FakeCorSubmissionRepository();
        _parkingScheduleRepository = new FakeParkingScheduleRepository();
        _studentRepository = new FakeStudentRepository();
        _personnelRepository = new FakePersonnelRepository();
        _adminRepository = new FakeAdminRepository();
        _violationRepository = new FakeViolationRepository();
        _parkingService = new FakeParkingServiceForExit();
        _scheduleService = new FakeScheduleService();
        _parkingLogRoleService = new ParkingLogRoleService();
    }

    [Fact]
    public async Task Handle_ShouldExitParkingLog_WhenVehicleAndActiveLogExist()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var guardUserId = Guid.NewGuid();
        var guardProfileId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var vehicle = new Vehicle(ownerId, "ABC-1234", "Toyota", "hashed_qr", VehicleType.Car);
        var vehicleIdProperty = typeof(BaseEntity).GetProperty("Id");
        vehicleIdProperty?.SetValue(vehicle, vehicleId);
        await _vehicleRepository.AddAsync(vehicle);

        var ownerAccount = new UserAccount(string.Empty, "+639999999999");
        var ownerProfile = new UserProfile(ownerId, "John", "Doe", null, null);
        ownerProfile.UserAccount = ownerAccount;
        _userProfileRepository.Profiles.Add(ownerProfile);

        // Link Vehicle Owner
        var ownerProperty = typeof(Vehicle).GetProperty("Owner");
        ownerProperty?.SetValue(vehicle, ownerAccount);

        var guardAccount = new UserAccount(string.Empty, "+639888888888");
        var guardProfile = new UserProfile(guardUserId, "Guard", "One", null, null);
        guardProfile.UserAccount = guardAccount;
        var guardProfileIdProperty = typeof(BaseEntity).GetProperty("Id");
        guardProfileIdProperty?.SetValue(guardProfile, guardProfileId);
        _userProfileRepository.Profiles.Add(guardProfile);

        var guard = new Guard(guardProfile, 1);
        _guardRepository.Guard = guard;

        var activeLog = new ParkingLog(vehicleId, guardProfileId, ParkingStatus.Parked, EntryMethod.Manual);
        // Set vehicle property on log
        var vehicleProperty = typeof(ParkingLog).GetProperty("Vehicle");
        vehicleProperty?.SetValue(activeLog, vehicle);

        var parkingLogRepository = new FakeParkingLogRepositoryForExit(activeLog);
        var fakeNotificationSender = new FakeSignalRNotificationSender();

        var handler = new ExitManualParkingLogHandler(
            parkingLogRepository,
            _vehicleRepository,
            _userProfileRepository,
            _guardRepository,
            _corSubmissionRepository,
            _parkingScheduleRepository,
            _studentRepository,
            _personnelRepository,
            _adminRepository,
            _violationRepository,
            _parkingService,
            new FakeViolationService(),
            _parkingLogRoleService,
            new ExitManualParkingLogValidator(),
            fakeNotificationSender
        );

        var command = new ExitManualParkingLogCommand("ABC-1234", guardUserId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("ABC-1234", result.Data.PlateNumber);
        Assert.Equal(ParkingStatus.Exited.ToString(), result.Data.Status);
        Assert.Equal(0, result.Data.PenaltyFee);
        Assert.False(fakeNotificationSender.WasNotificationSent);
    }
}

public class FakeParkingLogRepositoryForExit : IParkingLogRepository
{
    private readonly ParkingLog _activeLog;

    public FakeParkingLogRepositoryForExit(ParkingLog activeLog)
    {
        _activeLog = activeLog;
    }

    public Task AddParkingLogAsync(ParkingLog parkingLog) => Task.CompletedTask;
    public Task<ParkingLog?> GetActiveParkingLogByVehicleIdAsync(Guid vehicleId) => Task.FromResult<ParkingLog?>(_activeLog);
    public Task<IReadOnlyList<ParkingLog>> GetActiveParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetTodaysParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetRecentParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetParkingHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task UpdateParkingLogAsync(ParkingLog parkingLog) => Task.CompletedTask;
}

public class FakeSignalRNotificationSender : ISignalRNotificationSender
{
    public bool WasNotificationSent { get; private set; }

    public Task SendEventNotificationAsync(string userId, object data)
    {
        WasNotificationSent = true;
        return Task.CompletedTask;
    }

    public Task SendToUserAsync(string userId, string method, object data)
    {
        WasNotificationSent = true;
        return Task.CompletedTask;
    }

    public Task SendToAllAsync(string method, object data)
    {
        WasNotificationSent = true;
        return Task.CompletedTask;
    }
}

public class FakeParkingServiceForExit : IParkingService
{
    public ParkingLog CreateEntry(Guid vehicleId, Guid guardId, EntryMethod entryMethod = EntryMethod.QrCode)
    {
        return new ParkingLog(vehicleId, guardId, ParkingStatus.Parked, entryMethod);
    }
    public void MarkExit(ParkingLog parkingLog)
    {
        parkingLog.Exit();
    }
    public DateTime CalculateEntryGracePeriod(DateTime entryTime, TimeSpan scheduleStartTime, int graceMinutes = 30) => default;
    public DateTime CalculateEstimatedExitTime(DateTime entryTime, TimeSpan scheduleEndTime, int graceMinutes = 30) => default;
    public DateTime CalculateMaximumExitTime(DateTime entryTime, TimeSpan scheduleEndTime, int graceMinutes = 30) => default;
    public double CalculateTotalParkingHours(DateTime entryTime, DateTime exitTime) => 0.0;
}
