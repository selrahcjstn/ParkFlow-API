using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Features.ParkingLogs.Queries.VerifyStudentScan;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Violations;

namespace Test.Features.ParkingLogs;

public class VerifyStudentScanTests
{
    private readonly FakeTestStudentRepository _studentRepository;
    private readonly FakeTestUserProfileRepository _userProfileRepository;
    private readonly FakeTestCorSubmissionRepository _corSubmissionRepository;
    private readonly FakeTestParkingScheduleRepository _parkingScheduleRepository;
    private readonly FakeTestVehicleRepository _vehicleRepository;
    private readonly FakeParkingLogRepository _parkingLogRepository;
    private readonly FakeViolationRepository _violationRepository;
    private readonly IScheduleService _scheduleService;
    private readonly VerifyStudentScanHandler _handler;

    public VerifyStudentScanTests()
    {
        _studentRepository = new FakeTestStudentRepository();
        _userProfileRepository = new FakeTestUserProfileRepository();
        _corSubmissionRepository = new FakeTestCorSubmissionRepository();
        _parkingScheduleRepository = new FakeTestParkingScheduleRepository();
        _vehicleRepository = new FakeTestVehicleRepository();
        _parkingLogRepository = new FakeParkingLogRepository();
        _violationRepository = new FakeViolationRepository();
        _scheduleService = new ScheduleService();

        _handler = new VerifyStudentScanHandler(
            _studentRepository,
            _userProfileRepository,
            _corSubmissionRepository,
            _parkingScheduleRepository,
            _vehicleRepository,
            _parkingLogRepository,
            _violationRepository,
            _scheduleService
        );
    }

    [Fact]
    public async Task Handle_WhenStudentNotFound_ReturnsNotFoundStatusWithDetails()
    {
        // Act: scanning "2023-99999, John Unknown, BS Computer Science"
        var query = new VerifyStudentScanQuery(
            QrContent: "2023-99999, John Unknown, BS Computer Science"
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.IsValid);
        Assert.Equal("NotFound", result.Data.EntryStatus);
        Assert.Equal("2023-99999", result.Data.StudentNumber);
        Assert.Equal("John Unknown", result.Data.FullName);
        Assert.Contains("not registered", result.Data.StatusMessage);
    }

    [Fact]
    public async Task Handle_WhenStudentHasNoCor_ReturnsCorNotVerified()
    {
        // Arrange
        var userAccountId = Guid.NewGuid();
        var profile = new UserProfile(userAccountId, "Maria", "Santos", null, null);
        await _userProfileRepository.AddAsync(profile);

        var student = new Student(profile.Id, "2023-10001", "BS Information Technology", "3A", 3)
        {
            UserProfile = profile
        };
        await _studentRepository.AddAsync(student);

        // Act
        var query = new VerifyStudentScanQuery(StudentNumber: "2023-10001");
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.IsValid);
        Assert.Equal("CorNotVerified", result.Data.EntryStatus);
        Assert.Equal("Maria Santos", result.Data.FullName);
        Assert.Equal("BS Information Technology", result.Data.Program);
    }

    [Fact]
    public async Task Handle_WhenStudentHasNoScheduleForToday_ReturnsNoScheduleToday()
    {
        // Arrange
        var userAccountId = Guid.NewGuid();
        var profile = new UserProfile(userAccountId, "Carlos", "Reyes", null, null);
        await _userProfileRepository.AddAsync(profile);

        var student = new Student(profile.Id, "2023-10002", "BS Computer Engineering", "2B", 2)
        {
            UserProfile = profile
        };
        await _studentRepository.AddAsync(student);

        var cor = new CorSubmission(userAccountId, "1st Term 2026", "https://cor.pdf", verificationStatus: CorVerificationStatus.Verified);
        await _corSubmissionRepository.AddCorSubmissionAsync(cor);

        // Add schedule for a day that is NOT today
        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);
        var otherDay = (DayOfWeek)(((int)philippinesNow.DayOfWeek + 1) % 7);

        var schedule = new ParkingSchedule(cor.Id, otherDay, new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0));
        await _parkingScheduleRepository.AddAsync(schedule);

        // Act
        var query = new VerifyStudentScanQuery(QrContent: "2023-10002, Carlos Reyes, BS Computer Engineering");
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.IsValid);
        Assert.Equal("NoScheduleToday", result.Data.EntryStatus);
    }

    [Fact]
    public async Task Handle_WhenStudentHasValidScheduleNow_ReturnsApprovedWithVehicle()
    {
        // Arrange
        var userAccountId = Guid.NewGuid();
        var profile = new UserProfile(userAccountId, "Juan", "Dela Cruz", null, null);
        await _userProfileRepository.AddAsync(profile);

        var student = new Student(profile.Id, "2023-12345", "BS Information Technology", "4A", 4)
        {
            UserProfile = profile
        };
        await _studentRepository.AddAsync(student);

        var vehicle = new Vehicle(userAccountId, "ABC-1234", "Toyota", "HASH-123", VehicleType.Car);
        vehicle.MarkAsPrimary();
        await _vehicleRepository.AddAsync(vehicle);

        var cor = new CorSubmission(userAccountId, "1st Term 2026", "https://cor.pdf", verificationStatus: CorVerificationStatus.Verified);
        await _corSubmissionRepository.AddCorSubmissionAsync(cor);

        // Add schedule spanning current Philippines time
        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);
        var currentTod = philippinesNow.TimeOfDay;
        var start = currentTod.Subtract(TimeSpan.FromMinutes(10));
        var end = currentTod.Add(TimeSpan.FromHours(2));

        var schedule = new ParkingSchedule(cor.Id, philippinesNow.DayOfWeek, start, end);
        await _parkingScheduleRepository.AddAsync(schedule);

        // Act: scan with 3 fields
        var query = new VerifyStudentScanQuery(QrContent: "2023-12345, Juan Dela Cruz, BS Information Technology");
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsValid);
        Assert.Equal("Approved", result.Data.EntryStatus);
        Assert.Equal("Juan Dela Cruz", result.Data.FullName);
        Assert.Equal("2023-12345", result.Data.StudentNumber);
        Assert.True(result.Data.HasRegisteredVehicle);
        Assert.Equal("ABC-1234", result.Data.PlateNumber);
        Assert.Equal("Toyota", result.Data.VehicleBrand);
        Assert.Equal("HASH-123", result.Data.VehicleQrCodeHash);
    }
}

// Minimal fakes for test
public class FakeTestStudentRepository : IStudentRepository
{
    private readonly List<Student> _students = new();
    public Task AddAsync(Student student) { _students.Add(student); return Task.CompletedTask; }
    public Task UpdateAsync(Student student) => Task.CompletedTask;
    public Task DeleteAsync(Student student) { _students.Remove(student); return Task.CompletedTask; }
    public Task<Student?> GetByUserProfileIdAsync(Guid userProfileId) =>
        Task.FromResult(_students.FirstOrDefault(s => s.UserProfileId == userProfileId));
    public Task<Student?> GetByStudentNumberAsync(string studentNumber) =>
        Task.FromResult(_students.FirstOrDefault(s => s.StudentNumber.Equals(studentNumber.Trim(), StringComparison.OrdinalIgnoreCase)));
}

public class FakeTestUserProfileRepository : IUserProfileRepository
{
    private readonly List<UserProfile> _profiles = new();
    public Task AddAsync(UserProfile profile) { _profiles.Add(profile); return Task.CompletedTask; }
    public Task<UserProfile?> GetByIdAsync(Guid id) => Task.FromResult(_profiles.FirstOrDefault(p => p.Id == id));
    public Task<UserProfile?> GetByUserIdAsync(Guid userId) => Task.FromResult(_profiles.FirstOrDefault(p => p.UserAccountId == userId));
    public Task UpdateAsync(UserProfile profile) => Task.CompletedTask;
    public Task<UserProfile?> GetByEmailAsync(string email) => Task.FromResult<UserProfile?>(null);
    public Task<UserProfile?> GetByPhoneNumberAsync(string phoneNumber) => Task.FromResult<UserProfile?>(null);
}

public class FakeTestCorSubmissionRepository : ICorSubmissionRepository
{
    private readonly List<CorSubmission> _submissions = new();
    public Task AddCorSubmissionAsync(CorSubmission corSubmission) { _submissions.Add(corSubmission); return Task.CompletedTask; }
    public Task<CorSubmission?> GetCorSubmissionByIdAsync(Guid id) => Task.FromResult(_submissions.FirstOrDefault(s => s.Id == id));
    public Task<IEnumerable<CorSubmission>> ListCorSubmissionsAsync() => Task.FromResult<IEnumerable<CorSubmission>>(_submissions);
    public Task UpdateCorSubmissionAsync(CorSubmission corSubmission) => Task.CompletedTask;
    public Task<CorSubmission?> GetCorSubmissionAsync(Guid id) => Task.FromResult(_submissions.FirstOrDefault(s => s.Id == id));
    public Task<CorSubmission?> GetByUserIdAndTermAsync(Guid userId, string term) => Task.FromResult<CorSubmission?>(null);
    public Task<CorSubmission?> GetLatestByUserIdAsync(Guid userId) => Task.FromResult(_submissions.OrderByDescending(s => s.CreatedAt).FirstOrDefault(s => s.UserAccountId == userId));
    public Task DeleteCorSubmissionAsync(CorSubmission corSubmission) => Task.CompletedTask;
}

public class FakeTestParkingScheduleRepository : IParkingScheduleRepository
{
    private readonly List<ParkingSchedule> _schedules = new();
    public Task AddAsync(ParkingSchedule parkingSchedule) { _schedules.Add(parkingSchedule); return Task.CompletedTask; }
    public Task AddParkingScheduleAsync(ParkingSchedule parkingSchedule) => AddAsync(parkingSchedule);
    public Task<ParkingSchedule?> GetByIdAsync(Guid id) => Task.FromResult(_schedules.FirstOrDefault(s => s.Id == id));
    public Task<IEnumerable<ParkingSchedule>> GetBySubmissionIdAsync(Guid submissionId) =>
        Task.FromResult<IEnumerable<ParkingSchedule>>(_schedules.Where(s => s.SubmissionId == submissionId));
    public Task<IEnumerable<ParkingSchedule>> GetByUserIdAsync(Guid userId) => Task.FromResult<IEnumerable<ParkingSchedule>>(new List<ParkingSchedule>());
    public Task UpdateAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
    public Task UpdateParkingScheduleAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
    public Task DeleteAsync(ParkingSchedule parkingSchedule) => Task.CompletedTask;
    public Task ReplaceSchedulesAsync(Guid submissionId, IEnumerable<ParkingSchedule> schedules) => Task.CompletedTask;
}

public class FakeTestVehicleRepository : IVehicleRepository
{
    private readonly List<Vehicle> _vehicles = new();
    public Task AddAsync(Vehicle vehicle) { _vehicles.Add(vehicle); return Task.CompletedTask; }
    public Task<Vehicle?> GetByIdAsync(Guid id) => Task.FromResult(_vehicles.FirstOrDefault(v => v.Id == id));
    public Task<Vehicle?> GetByQrCodeHashAsync(string qrCodeHash) => Task.FromResult(_vehicles.FirstOrDefault(v => v.QrCodeHash == qrCodeHash));
    public Task<Vehicle?> GetByPlateNumberAsync(string plateNumber) => Task.FromResult(_vehicles.FirstOrDefault(v => v.PlateNumber == plateNumber));
    public Task<IEnumerable<Vehicle>> GetByOwnerIdAsync(Guid ownerId) =>
        Task.FromResult<IEnumerable<Vehicle>>(_vehicles.Where(v => v.OwnerId == ownerId));
    public Task<IEnumerable<Vehicle>> GetByOwnerIdsAsync(IEnumerable<Guid> ownerIds) =>
        Task.FromResult<IEnumerable<Vehicle>>(_vehicles.Where(v => ownerIds.Contains(v.OwnerId)));
    public Task<IEnumerable<Vehicle>> GetAllAsync() => Task.FromResult<IEnumerable<Vehicle>>(_vehicles);
    public Task UpdateAsync(Vehicle vehicle) => Task.CompletedTask;
    public Task DeleteAsync(Vehicle vehicle) => Task.CompletedTask;
}
