using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateManualParkingLog;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Auth;
using Test.Features.Violations;

namespace Test.Features.ParkingLogs;

public class ManualParkingLogTests
{
    private readonly FakeParkingLogRepository _parkingLogRepository;
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

    public ManualParkingLogTests()
    {
        _parkingLogRepository = new FakeParkingLogRepository();
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
        _parkingService = new FakeParkingServiceWithRealEntry();
        _scheduleService = new FakeScheduleService();
        _parkingLogRoleService = new ParkingLogRoleService();
    }

    [Fact]
    public async Task Handle_ShouldCreateParkingLog_WhenVehicleExistsAndIsValid()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var guardUserId = Guid.NewGuid();
        var guardProfileId = Guid.NewGuid();

        var vehicle = new Vehicle(ownerId, "ABC-1234", "Toyota", "hashed_qr", VehicleType.Car);
        var vehicleIdProperty = typeof(BaseEntity).GetProperty("Id");
        vehicleIdProperty?.SetValue(vehicle, Guid.NewGuid());
        await _vehicleRepository.AddAsync(vehicle);

        var ownerAccount = new UserAccount(string.Empty, "+639999999999");
        var ownerProfile = new UserProfile(ownerId, "John", "Doe", null, null);
        ownerProfile.UserAccount = ownerAccount;
        _userProfileRepository.Profiles.Add(ownerProfile);

        var guardAccount = new UserAccount(string.Empty, "+639888888888");
        var guardProfile = new UserProfile(guardUserId, "Guard", "One", null, null);
        guardProfile.UserAccount = guardAccount;
        var guardProfileIdProperty = typeof(BaseEntity).GetProperty("Id");
        guardProfileIdProperty?.SetValue(guardProfile, guardProfileId);
        _userProfileRepository.Profiles.Add(guardProfile);

        var guard = new Guard(guardProfile, 1);
        _guardRepository.Guard = guard;

        var cor = new CorSubmission(ownerId, "term-1", "doc-url");
        var corIdProperty = typeof(BaseEntity).GetProperty("Id");
        corIdProperty?.SetValue(cor, Guid.NewGuid());
        cor.UpdateSubmission(null, null, CorVerificationStatus.Verified);

        // Use reflection to mock the ListCorSubmissions to return our verified COR
        var corSubmissionsField = typeof(FakeCorSubmissionRepository).GetField("corSubmissions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fakeCorList = new List<CorSubmission> { cor };
        var corSubRepo = new FakeCorSubmissionRepositoryWithMock(fakeCorList);

        // Schedule setup
        var utcNow = DateTime.UtcNow;
        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(utcNow);
        var schedule = new ParkingSchedule(cor.Id, philippinesNow.DayOfWeek, new TimeSpan(6, 0, 0), new TimeSpan(20, 0, 0));
        var scheduleSubRepo = new FakeParkingScheduleRepositoryWithMock(new List<ParkingSchedule> { schedule });

        var handler = new CreateManualParkingLogHandler(
            _parkingLogRepository,
            _vehicleRepository,
            _userProfileRepository,
            _userAccountRepository,
            _guardRepository,
            _studentRepository,
            _personnelRepository,
            _adminRepository,
            _violationRepository,
            _parkingService,
            _scheduleService,
            _parkingLogRoleService
        );

        var command = new CreateManualParkingLogCommand("ABC-1234", VehicleType.Car, "+639999999999", "Toyota", guardUserId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("ABC-1234", result.Data.PlateNumber);
        Assert.Equal("Manual", result.Data.EntryMethod);
    }

    [Fact]
    public async Task Handle_ShouldAutoRegisterVehicleAndCreateParkingLog_WhenVehicleDoesNotExistButPhoneNumberMatchesUser()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var guardUserId = Guid.NewGuid();
        var guardProfileId = Guid.NewGuid();

        var ownerAccount = new UserAccount(string.Empty, "+639999999999");
        var ownerProfile = new UserProfile(ownerId, "John", "Doe", null, null);
        ownerProfile.UserAccount = ownerAccount;
        _userProfileRepository.Profiles.Add(ownerProfile);
        
        var idField = typeof(BaseEntity).GetProperty("Id");
        idField?.SetValue(ownerAccount, ownerId);
        await _userAccountRepository.AddAsync(ownerAccount);

        var guardAccount = new UserAccount(string.Empty, "+639888888888");
        var guardProfile = new UserProfile(guardUserId, "Guard", "One", null, null);
        guardProfile.UserAccount = guardAccount;
        var guardProfileIdProperty = typeof(BaseEntity).GetProperty("Id");
        guardProfileIdProperty?.SetValue(guardProfile, guardProfileId);
        _userProfileRepository.Profiles.Add(guardProfile);

        var guard = new Guard(guardProfile, 1);
        _guardRepository.Guard = guard;

        var cor = new CorSubmission(ownerId, "term-1", "doc-url");
        var corIdProperty = typeof(BaseEntity).GetProperty("Id");
        corIdProperty?.SetValue(cor, Guid.NewGuid());
        cor.UpdateSubmission(null, null, CorVerificationStatus.Verified);
        var corSubRepo = new FakeCorSubmissionRepositoryWithMock(new List<CorSubmission> { cor });

        // Schedule setup
        var utcNow = DateTime.UtcNow;
        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(utcNow);
        var schedule = new ParkingSchedule(cor.Id, philippinesNow.DayOfWeek, new TimeSpan(6, 0, 0), new TimeSpan(20, 0, 0));
        var scheduleSubRepo = new FakeParkingScheduleRepositoryWithMock(new List<ParkingSchedule> { schedule });

        var handler = new CreateManualParkingLogHandler(
            _parkingLogRepository,
            _vehicleRepository,
            _userProfileRepository,
            _userAccountRepository,
            _guardRepository,
            _studentRepository,
            _personnelRepository,
            _adminRepository,
            _violationRepository,
            _parkingService,
            _scheduleService,
            _parkingLogRoleService
        );

        var command = new CreateManualParkingLogCommand("NEW-PLATE", VehicleType.Car, "+639999999999", "Honda", guardUserId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("NEW-PLATE", result.Data.PlateNumber);
        Assert.Equal("Manual", result.Data.EntryMethod);

        // Verify vehicle was registered
        var registeredVehicle = await _vehicleRepository.GetByPlateNumberAsync("NEW-PLATE");
        Assert.NotNull(registeredVehicle);
        Assert.Equal(ownerId, registeredVehicle.OwnerId);
        Assert.Equal("Honda", registeredVehicle.Brand);
    }

    [Fact]
    public async Task Handle_ShouldAutoRegisterGuest_WhenVehicleDoesNotExistAndNoPhoneNumberMatchesUser()
    {
        // Arrange
        var guardUserId = Guid.NewGuid();
        var guardProfileId = Guid.NewGuid();

        var guardAccount = new UserAccount(string.Empty, "+639888888888");
        var guardProfile = new UserProfile(guardUserId, "Guard", "One", null, null);
        guardProfile.UserAccount = guardAccount;
        var guardProfileIdProperty = typeof(BaseEntity).GetProperty("Id");
        guardProfileIdProperty?.SetValue(guardProfile, guardProfileId);
        _userProfileRepository.Profiles.Add(guardProfile);

        var guard = new Guard(guardProfile, 1);
        _guardRepository.Guard = guard;

        var handler = new CreateManualParkingLogHandler(
            _parkingLogRepository,
            _vehicleRepository,
            _userProfileRepository,
            _userAccountRepository,
            _guardRepository,
            _studentRepository,
            _personnelRepository,
            _adminRepository,
            _violationRepository,
            _parkingService,
            _scheduleService,
            _parkingLogRoleService
        );

        var command = new CreateManualParkingLogCommand("UNKNOWN-PLATE", VehicleType.Car, "+639999999999", "Honda", guardUserId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("UNKNOWN-PLATE", result.Data.PlateNumber);
        Assert.Equal("Manual", result.Data.EntryMethod);

        // Verify guest account and vehicle were created
        var guestAccount = await _userAccountRepository.GetByPhoneNumberAsync("+00000000000");
        Assert.NotNull(guestAccount);
        Assert.Equal(AccountStatus.Active, guestAccount.Status);

        var registeredVehicle = await _vehicleRepository.GetByPlateNumberAsync("UNKNOWN-PLATE");
        Assert.NotNull(registeredVehicle);
        Assert.Equal(guestAccount.Id, registeredVehicle.OwnerId);
        Assert.Equal("Honda", registeredVehicle.Brand);
    }
}

// Helpers to return mock data for submission list and schedules
public class FakeCorSubmissionRepositoryWithMock : ICorSubmissionRepository
{
    private readonly IEnumerable<CorSubmission> _submissions;
    public FakeCorSubmissionRepositoryWithMock(IEnumerable<CorSubmission> submissions)
    {
        _submissions = submissions;
    }
    public Task AddCorSubmissionAsync(CorSubmission corSubmission) => Task.CompletedTask;
    public Task<CorSubmission?> GetCorSubmissionByIdAsync(Guid id) => Task.FromResult<CorSubmission?>(null);
    public Task<IEnumerable<CorSubmission>> ListCorSubmissionsAsync() => Task.FromResult(_submissions);
    public Task UpdateCorSubmissionAsync(CorSubmission corSubmission) => Task.CompletedTask;
    public Task<CorSubmission?> GetCorSubmissionAsync(Guid id) => Task.FromResult<CorSubmission?>(null);
    public Task<CorSubmission?> GetByUserIdAndTermAsync(Guid userId, string term) => Task.FromResult<CorSubmission?>(null);
    public Task<CorSubmission?> GetLatestByUserIdAsync(Guid userId) =>
        Task.FromResult(_submissions.OrderByDescending(s => s.CreatedAt).FirstOrDefault(s => s.UserAccountId == userId));
    public Task DeleteCorSubmissionAsync(CorSubmission corSubmission) => Task.CompletedTask;
}

public class FakeParkingScheduleRepositoryWithMock : IParkingScheduleRepository
{
    private readonly IEnumerable<ParkingSchedule> _schedules;
    public FakeParkingScheduleRepositoryWithMock(IEnumerable<ParkingSchedule> schedules)
    {
        _schedules = schedules;
    }
    public Task AddParkingScheduleAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
    public Task AddAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
    public Task<ParkingSchedule?> GetByIdAsync(Guid id) => Task.FromResult<ParkingSchedule?>(null);
    public Task<IEnumerable<ParkingSchedule>> GetBySubmissionIdAsync(Guid submissionId) => Task.FromResult(_schedules);
    public Task<IEnumerable<ParkingSchedule>> GetByUserIdAsync(Guid userId) => Task.FromResult<IEnumerable<ParkingSchedule>>(new List<ParkingSchedule>());
    public Task UpdateParkingScheduleAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
    public Task UpdateAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
    public Task DeleteAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
    public Task ReplaceSchedulesAsync(Guid submissionId, IEnumerable<ParkingSchedule> schedules) => Task.CompletedTask;
}

public class FakeParkingServiceWithRealEntry : IParkingService
{
    public ParkingLog CreateEntry(Guid vehicleId, Guid guardId, EntryMethod entryMethod = EntryMethod.QrCode)
    {
        return new ParkingLog(vehicleId, guardId, ParkingStatus.Parked, entryMethod);
    }
    public void MarkExit(ParkingLog parkingLog) { }
    public DateTime CalculateEntryGracePeriod(DateTime entryTime, TimeSpan scheduleStartTime, int graceMinutes = 30) => default;
    public DateTime CalculateEstimatedExitTime(DateTime entryTime, TimeSpan scheduleEndTime, int graceMinutes = 30) => default;
    public DateTime CalculateMaximumExitTime(DateTime entryTime, TimeSpan scheduleEndTime, int graceMinutes = 30) => default;
    public double CalculateTotalParkingHours(DateTime entryTime, DateTime exitTime) => 0.0;
}
