using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Features.Reservations.Queries.VerifyReservationScan;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Violations;

namespace Test.Features.ParkingLogs;

public class ScheduleRestrictionEntryTests
{
    private readonly FakeTestStudentRepository _studentRepository;
    private readonly FakeTestUserProfileRepository _userProfileRepository;
    private readonly FakeTestCorSubmissionRepository _corSubmissionRepository;
    private readonly FakeTestParkingScheduleRepository _parkingScheduleRepository;
    private readonly FakeTestVehicleRepository _vehicleRepository;
    private readonly FakeParkingLogRepository _parkingLogRepository;
    private readonly FakeViolationRepository _violationRepository;
    private readonly FakeGuardRepository _guardRepository;
    private readonly FakePersonnelRepository _personnelRepository;
    private readonly FakeAdminRepository _adminRepository;
    private readonly FakeTestParkingReservationRepository _reservationRepository;
    private readonly IScheduleService _scheduleService;
    private readonly IParkingService _parkingService;
    private readonly IParkingLogRoleService _parkingLogRoleService;

    public ScheduleRestrictionEntryTests()
    {
        _studentRepository = new FakeTestStudentRepository();
        _userProfileRepository = new FakeTestUserProfileRepository();
        _corSubmissionRepository = new FakeTestCorSubmissionRepository();
        _parkingScheduleRepository = new FakeTestParkingScheduleRepository();
        _vehicleRepository = new FakeTestVehicleRepository();
        _parkingLogRepository = new FakeParkingLogRepository();
        _violationRepository = new FakeViolationRepository();
        _guardRepository = new FakeGuardRepository();
        _personnelRepository = new FakePersonnelRepository();
        _adminRepository = new FakeAdminRepository();
        _reservationRepository = new FakeTestParkingReservationRepository();
        _scheduleService = new ScheduleService();
        _parkingService = new ParkingService();
        _parkingLogRoleService = new ParkingLogRoleService();
    }

    [Fact]
    public async Task CreateParkingLog_WhenClassScheduleEndedToday_ReturnsForbidden()
    {
        // Arrange
        var guardUserAccountId = Guid.NewGuid();
        var guardProfile = new UserProfile(guardUserAccountId, "Guard", "One", null, null);
        await _userProfileRepository.AddAsync(guardProfile);
        var guard = new Guard(guardProfile, 1);
        _guardRepository.Guard = guard;

        var studentUserAccountId = Guid.NewGuid();
        var studentProfile = new UserProfile(studentUserAccountId, "Student", "User", null, null);
        await _userProfileRepository.AddAsync(studentProfile);
        var student = new Student(studentProfile.Id, "2023-0001", "BSCS", "4A", 4) { UserProfile = studentProfile };
        await _studentRepository.AddAsync(student);

        var vehicle = new Vehicle(studentUserAccountId, "ABC-1234", "Toyota", "HASH-ENDED-CLASS", VehicleType.Car);
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
        await _vehicleRepository.AddAsync(vehicle);

        var cor = new CorSubmission(studentUserAccountId, "1st Term 2026", "https://cor.pdf", verificationStatus: CorVerificationStatus.Verified);
        await _corSubmissionRepository.AddCorSubmissionAsync(cor);

        // Schedule that ended 30 minutes ago
        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);
        var currentTod = philippinesNow.TimeOfDay;
        var end = currentTod.Subtract(TimeSpan.FromMinutes(30));
        var start = end.Subtract(TimeSpan.FromHours(2));

        var schedule = new ParkingSchedule(cor.Id, philippinesNow.DayOfWeek, start, end);
        await _parkingScheduleRepository.AddAsync(schedule);

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
            _parkingLogRoleService,
            new FakeSignalRNotificationSender(),
            _reservationRepository
        );

        var command = new CreateParkingLogCommand("HASH-ENDED-CLASS", guardUserAccountId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Contains("Scheduled classes for today ended at", result.Message);
    }

    [Fact]
    public async Task CreateParkingLog_WhenReservationEndedToday_ReturnsForbidden()
    {
        // Arrange
        var guardUserAccountId = Guid.NewGuid();
        var guardProfile = new UserProfile(guardUserAccountId, "Guard", "One", null, null);
        await _userProfileRepository.AddAsync(guardProfile);
        var guard = new Guard(guardProfile, 1);
        _guardRepository.Guard = guard;

        var studentUserAccountId = Guid.NewGuid();
        var studentProfile = new UserProfile(studentUserAccountId, "Student", "User", null, null);
        await _userProfileRepository.AddAsync(studentProfile);

        var vehicle = new Vehicle(studentUserAccountId, "XYZ-9999", "Honda", "HASH-ENDED-RES", VehicleType.Car);
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
        await _vehicleRepository.AddAsync(vehicle);

        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);
        var currentTod = philippinesNow.TimeOfDay;
        var resEnd = currentTod.Subtract(TimeSpan.FromMinutes(20));
        var resStart = resEnd.Subtract(TimeSpan.FromHours(2));

        var reservation = new ParkingReservation(
            studentUserAccountId,
            "RES-TEST-001",
            philippinesNow.Date,
            resStart,
            resEnd,
            "Attending workshop",
            ReservationType.Normal,
            vehicle.Id
        );
        reservation.Approve(Guid.NewGuid());
        await _reservationRepository.AddAsync(reservation);

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
            _parkingLogRoleService,
            new FakeSignalRNotificationSender(),
            _reservationRepository
        );

        var command = new CreateParkingLogCommand("HASH-ENDED-RES", guardUserAccountId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Contains("Reservation schedule for today ended at", result.Message);
    }

    [Fact]
    public async Task CreateParkingLog_WhenReservationCompleted_ReturnsForbidden()
    {
        // Arrange
        var guardUserAccountId = Guid.NewGuid();
        var guardProfile = new UserProfile(guardUserAccountId, "Guard", "One", null, null);
        await _userProfileRepository.AddAsync(guardProfile);
        var guard = new Guard(guardProfile, 1);
        _guardRepository.Guard = guard;

        var studentUserAccountId = Guid.NewGuid();
        var studentProfile = new UserProfile(studentUserAccountId, "Student", "User", null, null);
        await _userProfileRepository.AddAsync(studentProfile);

        var vehicle = new Vehicle(studentUserAccountId, "XYZ-9999", "Honda", "HASH-COMPLETED-RES", VehicleType.Car);
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
        await _vehicleRepository.AddAsync(vehicle);

        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);
        var resStart = philippinesNow.TimeOfDay.Subtract(TimeSpan.FromHours(1));
        var resEnd = philippinesNow.TimeOfDay.Add(TimeSpan.FromHours(2));

        var reservation = new ParkingReservation(
            studentUserAccountId,
            "RES-TEST-002",
            philippinesNow.Date,
            resStart,
            resEnd,
            "Attending workshop",
            ReservationType.Normal,
            vehicle.Id
        );
        reservation.Approve(Guid.NewGuid());
        reservation.MarkCompleted();
        await _reservationRepository.AddAsync(reservation);

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
            _parkingLogRoleService,
            new FakeSignalRNotificationSender(),
            _reservationRepository
        );

        var command = new CreateParkingLogCommand("HASH-COMPLETED-RES", guardUserAccountId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Contains("already been used and is now void", result.Message);
    }
}

public class FakeTestParkingReservationRepository : IParkingReservationRepository
{
    public Task<ParkFlow.Application.Features.Reservations.Queries.GetCalendarReservations.CalendarReservationPage> GetCalendarPageAsync(DateTime date, DateTime month, int page, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    private readonly List<ParkingReservation> _reservations = new();

    public Task AddAsync(ParkingReservation reservation)
    {
        _reservations.Add(reservation);
        return Task.CompletedTask;
    }

    public Task<ParkingReservation?> GetByIdAsync(Guid id) =>
        Task.FromResult(_reservations.FirstOrDefault(r => r.Id == id));

    public Task<IEnumerable<ParkingReservation>> GetByUserIdAsync(Guid userId) =>
        Task.FromResult<IEnumerable<ParkingReservation>>(_reservations.Where(r => r.UserId == userId));

    public Task<ParkingReservation?> GetByReferenceNumberAsync(string referenceNumber) =>
        Task.FromResult(_reservations.FirstOrDefault(r => r.ReferenceNumber == referenceNumber));

    public Task<IEnumerable<ParkingReservation>> GetAllAsync(ReservationStatus? status = null) =>
        Task.FromResult<IEnumerable<ParkingReservation>>(
            status.HasValue ? _reservations.Where(r => r.Status == status.Value) : _reservations
        );

    public Task UpdateAsync(ParkingReservation reservation) => Task.CompletedTask;
    public Task SaveChangesAsync() => Task.CompletedTask;
}
