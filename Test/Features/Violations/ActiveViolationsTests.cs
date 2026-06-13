using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Features.Violations.DTOs;
using ParkFlow.Application.Features.Violations.Queries.GetUserViolations;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace Test.Features.Violations;

public class FakeViolationRepository : IViolationRepository
{
    public List<Violation> Violations { get; } = new();
    public bool HasActiveViolationValue { get; set; }

    public Task AddAsync(Violation violation)
    {
        Violations.Add(violation);
        return Task.CompletedTask;
    }

    public Task<Violation?> GetByIdAsync(Guid id) => Task.FromResult(Violations.FirstOrDefault(v => v.Id == id));
    public Task<Violation?> GetByLogIdAsync(Guid logId) => Task.FromResult(Violations.FirstOrDefault(v => v.ParkingLogId == logId));
    public Task<Violation?> GetByReferenceNumberAsync(string referenceNumber) => Task.FromResult(Violations.FirstOrDefault(v => v.ReferenceNumber == referenceNumber));
    public Task<IReadOnlyList<Violation>> GetRecentViolationsAsync(int limit) => Task.FromResult<IReadOnlyList<Violation>>(Violations.Take(limit).ToList());
    public Task<IReadOnlyList<Violation>> GetViolationHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15) => Task.FromResult<IReadOnlyList<Violation>>(Violations.ToList());

    public Task<IReadOnlyList<Violation>> GetViolationsByUserIdAsync(Guid userId)
    {
        return Task.FromResult<IReadOnlyList<Violation>>(
            Violations.Where(v => v.ParkingLog.Vehicle.OwnerId == userId).ToList()
        );
    }

    public Task<bool> HasActiveViolationAsync(Guid vehicleId)
    {
        return Task.FromResult(HasActiveViolationValue);
    }

    public Task UpdateAsync(Violation violation) => Task.CompletedTask;
    public Task DeleteAsync(Violation violation) => Task.CompletedTask;
}

public class FakeVehicleRepository : IVehicleRepository
{
    public List<Vehicle> Vehicles { get; } = new();

    public Task AddAsync(Vehicle vehicle)
    {
        Vehicles.Add(vehicle);
        return Task.CompletedTask;
    }

    public Task<Vehicle?> GetByIdAsync(Guid id) => Task.FromResult(Vehicles.FirstOrDefault(v => v.Id == id));
    public Task<Vehicle?> GetByQrCodeHashAsync(string qrCodeHash) => Task.FromResult(Vehicles.FirstOrDefault(v => v.QrCodeHash == qrCodeHash));
    public Task<Vehicle?> GetByPlateNumberAsync(string plateNumber) => Task.FromResult(Vehicles.FirstOrDefault(v => v.PlateNumber.Equals(plateNumber, StringComparison.OrdinalIgnoreCase)));
    public Task<IEnumerable<Vehicle>> GetByOwnerIdAsync(Guid ownerId) => Task.FromResult<IEnumerable<Vehicle>>(Vehicles.Where(v => v.OwnerId == ownerId).ToList());
    public Task<IEnumerable<Vehicle>> GetByOwnerIdsAsync(IEnumerable<Guid> ownerIds) => Task.FromResult<IEnumerable<Vehicle>>(Vehicles.Where(v => ownerIds.Contains(v.OwnerId)).ToList());
    public Task UpdateAsync(Vehicle vehicle) => Task.CompletedTask;
    public Task DeleteAsync(Vehicle vehicle) => Task.CompletedTask;
}

public class FakeUserProfileRepository : IUserProfileRepository
{
    public List<UserProfile> Profiles { get; } = new();

    public Task AddAsync(UserProfile userProfile)
    {
        Profiles.Add(userProfile);
        return Task.CompletedTask;
    }
    public Task<UserProfile?> GetByIdAsync(Guid id) => Task.FromResult(Profiles.FirstOrDefault(p => p.Id == id));
    public Task<UserProfile?> GetByUserIdAsync(Guid userId) => Task.FromResult(Profiles.FirstOrDefault(p => p.UserAccountId == userId));
    public Task UpdateAsync(UserProfile userProfile) => Task.CompletedTask;
}

public class FakeGuardRepository : IGuardRepository
{
    public Guard? Guard { get; set; }
    public Task<Guard?> GetByIdAsync(Guid id) => Task.FromResult(Guard);
    public Task<Guard?> GetByUserProfileIdAsync(Guid userProfileId) => Task.FromResult(Guard);
    public Task AddAsync(Guard guard) => Task.CompletedTask;
}

public class FakeAdminRepository : IAdminRepository
{
    public Admin? Admin { get; set; }
    public Task<Admin?> GetByIdAsync(Guid id) => Task.FromResult(Admin);
    public Task<Admin?> GetByUserProfileIdAsync(Guid userProfileId) => Task.FromResult(Admin);
    public Task AddAsync(Admin admin) => Task.CompletedTask;
    public Task<IEnumerable<Admin>> ListAllAsync() => Task.FromResult<IEnumerable<Admin>>(Admin != null ? new[] { Admin } : Array.Empty<Admin>());
}

public class FakeParkingLogRepository : IParkingLogRepository
{
    public Task AddParkingLogAsync(ParkingLog parkingLog) => Task.CompletedTask;
    public Task<ParkingLog?> GetActiveParkingLogByVehicleIdAsync(Guid vehicleId) => Task.FromResult<ParkingLog?>(null);
    public Task<IReadOnlyList<ParkingLog>> GetActiveParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetTodaysParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetRecentParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetParkingHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task UpdateParkingLogAsync(ParkingLog parkingLog) => Task.CompletedTask;
}

public class FakeCorSubmissionRepository : ICorSubmissionRepository
{
    public Task AddCorSubmissionAsync(CorSubmission corSubmission) => Task.CompletedTask;
    public Task<CorSubmission?> GetCorSubmissionByIdAsync(Guid id) => Task.FromResult<CorSubmission?>(null);
    public Task<IEnumerable<CorSubmission>> ListCorSubmissionsAsync() => Task.FromResult<IEnumerable<CorSubmission>>(new List<CorSubmission>());
    public Task UpdateCorSubmissionAsync(CorSubmission corSubmission) => Task.CompletedTask;
    public Task<CorSubmission?> GetCorSubmissionAsync(Guid id) => Task.FromResult<CorSubmission?>(null);
    public Task<CorSubmission?> GetByUserIdAndTermAsync(Guid userId, string term) => Task.FromResult<CorSubmission?>(null);
    public Task<CorSubmission?> GetLatestByUserIdAsync(Guid userId) => Task.FromResult<CorSubmission?>(null);
    public Task DeleteCorSubmissionAsync(CorSubmission corSubmission) => Task.CompletedTask;
}

public class FakeParkingScheduleRepository : IParkingScheduleRepository
{
    public Task AddParkingScheduleAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
    public Task AddAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
    public Task<ParkingSchedule?> GetByIdAsync(Guid id) => Task.FromResult<ParkingSchedule?>(null);
    public Task<IEnumerable<ParkingSchedule>> GetBySubmissionIdAsync(Guid submissionId) => Task.FromResult<IEnumerable<ParkingSchedule>>(new List<ParkingSchedule>());
    public Task<IEnumerable<ParkingSchedule>> GetByUserIdAsync(Guid userId) => Task.FromResult<IEnumerable<ParkingSchedule>>(new List<ParkingSchedule>());
    public Task UpdateParkingScheduleAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
    public Task UpdateAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
    public Task DeleteAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
}

public class FakeStudentRepository : IStudentRepository
{
    public Task<Student?> GetByUserProfileIdAsync(Guid userProfileId) => Task.FromResult<Student?>(null);
    public Task AddAsync(Student student) => Task.CompletedTask;
    public Task UpdateAsync(Student student) => Task.CompletedTask;
    public Task<Student?> GetByStudentNumberAsync(string studentNumber) => Task.FromResult<Student?>(null);
}

public class FakePersonnelRepository : IPersonnelRepository
{
    public Task<Personnel?> GetByUserProfileIdAsync(Guid userProfileId) => Task.FromResult<Personnel?>(null);
    public Task AddAsync(Personnel personnel) => Task.CompletedTask;
    public Task UpdateAsync(Personnel personnel) => Task.CompletedTask;
    public Task<Personnel?> GetByIdCardNumberAsync(string idCardNumber) => Task.FromResult<Personnel?>(null);
}

public class FakeParkingService : IParkingService
{
    public ParkingLog CreateEntry(Guid vehicleId, Guid guardId, EntryMethod entryMethod = EntryMethod.QrCode) => null!;
    public void MarkExit(ParkingLog parkingLog) { }
    public DateTime CalculateEntryGracePeriod(DateTime entryTime, TimeSpan scheduleStartTime, int graceMinutes = 30) => default;
    public DateTime CalculateEstimatedExitTime(DateTime entryTime, TimeSpan scheduleEndTime, int graceMinutes = 30) => default;
    public DateTime CalculateMaximumExitTime(DateTime entryTime, TimeSpan scheduleEndTime, int graceMinutes = 30) => default;
    public double CalculateTotalParkingHours(DateTime entryTime, DateTime exitTime) => 0.0;
}

public class FakeScheduleService : IScheduleService
{
    public bool CanEnter(DateTime currentTime, ParkingSchedule schedule, int graceMinutes = 30) => true;
    public TimeSpan GetEarliestAllowedEntryTime(ParkingSchedule schedule, int graceMinutes = 30) => default;
}

public class ActiveViolationsTests
{
    private readonly FakeViolationRepository _violationRepository;
    private readonly FakeVehicleRepository _vehicleRepository;
    private readonly FakeUserProfileRepository _userProfileRepository;
    private readonly FakeGuardRepository _guardRepository;
    private readonly FakeAdminRepository _adminRepository;
    private readonly FakeParkingLogRepository _parkingLogRepository;
    private readonly FakeCorSubmissionRepository _corSubmissionRepository;
    private readonly FakeParkingScheduleRepository _parkingScheduleRepository;
    private readonly FakeStudentRepository _studentRepository;
    private readonly FakePersonnelRepository _personnelRepository;
    private readonly FakeParkingService _parkingService;
    private readonly FakeScheduleService _scheduleService;
    private readonly ParkingLogRoleService _parkingLogRoleService;

    public ActiveViolationsTests()
    {
        _violationRepository = new FakeViolationRepository();
        _vehicleRepository = new FakeVehicleRepository();
        _userProfileRepository = new FakeUserProfileRepository();
        _guardRepository = new FakeGuardRepository();
        _adminRepository = new FakeAdminRepository();
        _parkingLogRepository = new FakeParkingLogRepository();
        _corSubmissionRepository = new FakeCorSubmissionRepository();
        _parkingScheduleRepository = new FakeParkingScheduleRepository();
        _studentRepository = new FakeStudentRepository();
        _personnelRepository = new FakePersonnelRepository();
        _parkingService = new FakeParkingService();
        _scheduleService = new FakeScheduleService();
        _parkingLogRoleService = new ParkingLogRoleService();
    }

    [Fact]
    public async Task CreateParkingLog_ShouldReturnForbidden_WhenVehicleHasActiveViolation()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var vehicle = new Vehicle(ownerId, "XYZ-789", "BMW", "hashed_qr", VehicleType.Car);
        var vehicleIdProperty = typeof(BaseEntity).GetProperty("Id");
        vehicleIdProperty?.SetValue(vehicle, Guid.NewGuid());
        await _vehicleRepository.AddAsync(vehicle);

        var ownerAccount = new UserAccount(string.Empty, "+639999999999");
        var ownerProfile = new UserProfile(ownerId, "John", "Doe", null, null);
        ownerProfile.UserAccount = ownerAccount;
        _userProfileRepository.Profiles.Add(ownerProfile);

        _violationRepository.HasActiveViolationValue = true;

        var command = new CreateParkingLogCommand("hashed_qr", Guid.NewGuid());
        var handler = new CreateParkingLogHandler(
            _parkingLogRepository,
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
            _scheduleService,
            _parkingLogRoleService
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal("Vehicle has active/unpaid violations. Entry denied.", result.Message);
    }

    [Fact]
    public async Task GetUserViolations_ShouldReturnAllViolations()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var vehicle = new Vehicle(ownerId, "XYZ-789", "BMW", "hashed_qr", VehicleType.Car);
        var vehicleIdProperty = typeof(BaseEntity).GetProperty("Id");
        vehicleIdProperty?.SetValue(vehicle, Guid.NewGuid());

        var ownerAccount = new UserAccount(string.Empty, "+639999999999");
        var ownerProfile = new UserProfile(ownerId, "John", "Doe", null, null);
        ownerProfile.UserAccount = ownerAccount;
        
        // Setup references on owner property of vehicle
        var ownerProp = typeof(Vehicle).GetProperty("Owner");
        ownerProp?.SetValue(vehicle, ownerAccount);
        ownerAccount.UserProfile = ownerProfile;

        _userProfileRepository.Profiles.Add(ownerProfile);

        // Create violation structure
        var log = new ParkingLog(vehicle.Id, Guid.NewGuid(), ParkingStatus.Parked);
        var logIdProperty = typeof(BaseEntity).GetProperty("Id");
        logIdProperty?.SetValue(log, Guid.NewGuid());

        // Set Vehicle on ParkingLog
        var logVehicleProp = typeof(ParkingLog).GetProperty("Vehicle");
        logVehicleProp?.SetValue(log, vehicle);

        // First violation - unpaid
        var violationUnpaid = new Violation(log.Id, 100.00m);
        var violLogProp1 = typeof(Violation).GetProperty("ParkingLog");
        violLogProp1?.SetValue(violationUnpaid, log);
        await _violationRepository.AddAsync(violationUnpaid);

        // Second violation - paid
        var violationPaid = new Violation(log.Id, 50.00m);
        var violLogProp2 = typeof(Violation).GetProperty("ParkingLog");
        violLogProp2?.SetValue(violationPaid, log);
        violationPaid.MarkAsPaid();
        await _violationRepository.AddAsync(violationPaid);

        var query = new GetUserViolationsQuery(ownerId);
        var handler = new GetUserViolationsHandler(
            _violationRepository,
            _userProfileRepository,
            _guardRepository,
            _adminRepository,
            _parkingLogRoleService
        );

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        var list = result.Data.ToList();
        Assert.Equal(2, list.Count);

        var unpaidResult = list.First(v => v.ViolationId == violationUnpaid.Id);
        Assert.Equal("XYZ-789", unpaidResult.PlateNumber);
        Assert.Equal(100.00m, unpaidResult.PenaltyFee);
        Assert.False(unpaidResult.IsPaid);

        var paidResult = list.First(v => v.ViolationId == violationPaid.Id);
        Assert.Equal("XYZ-789", paidResult.PlateNumber);
        Assert.Equal(50.00m, paidResult.PenaltyFee);
        Assert.True(paidResult.IsPaid);
    }
}
