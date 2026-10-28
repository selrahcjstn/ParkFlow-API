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
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

using Test.Features.ParkingLogs;

namespace Test.Support;

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
    public Task<IEnumerable<Vehicle>> GetAllAsync() => Task.FromResult<IEnumerable<Vehicle>>(Vehicles.ToList());
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
    public Task UpdateAsync(Guard guard) => Task.CompletedTask;
    public Task DeleteAsync(Guard guard) => Task.CompletedTask;
}

public class FakeAdminRepository : IAdminRepository
{
    public Admin? Admin { get; set; }
    public Task<Admin?> GetByIdAsync(Guid id) => Task.FromResult(Admin);
    public Task<Admin?> GetByUserProfileIdAsync(Guid userProfileId) => Task.FromResult(Admin);
    public Task AddAsync(Admin admin) => Task.CompletedTask;
    public Task DeleteAsync(Admin admin) => Task.CompletedTask;
    public Task<IEnumerable<Admin>> ListAllAsync() => Task.FromResult<IEnumerable<Admin>>(Admin != null ? new[] { Admin } : Array.Empty<Admin>());
}

public class FakeParkingLogRepository : IParkingLogRepository
{
    public ParkingLog? ActiveParkingLog { get; set; }
    public List<ParkingLog> CompletedExits { get; } = [];
    public List<ParkingReservation> CompletedReservations { get; } = [];
    public Task<bool> CompleteExitAsync(ParkingLog log, IReadOnlyCollection<ParkingReservation> reservations, CancellationToken ct = default)
    {
        if (ActiveParkingLog?.Id != log.Id) return Task.FromResult(false);
        CompletedExits.Add(log);
        CompletedReservations.AddRange(reservations);
        ActiveParkingLog = null;
        return Task.FromResult(true);
    }
    public Task AddParkingLogAsync(ParkingLog parkingLog) { ActiveParkingLog = parkingLog; return Task.CompletedTask; }
    public Task<ParkingLog?> GetActiveParkingLogByVehicleIdAsync(Guid vehicleId) => Task.FromResult(ActiveParkingLog?.VehicleId == vehicleId ? ActiveParkingLog : null);
    public Task<bool> HasActiveParkingLogByUserIdAsync(Guid userId) => Task.FromResult(false);
    public Task<IReadOnlyList<ParkingLog>> GetActiveParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetTodaysParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetRecentParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetParkingHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task UpdateParkingLogAsync(ParkingLog parkingLog) => Task.CompletedTask;
}

public static class DriverDocumentFixture
{
    public static void Approve(UserProfile profile)
    {
        profile.UpdateProfile(profile.FirstName, profile.LastName, profile.MiddleName, "uploads/profile.jpg");
        profile.SubmitLicense("uploads/license.jpg");
        profile.ReviewLicense(true, Guid.NewGuid(), "License checked for this fixture.");
    }
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
    public Task ReplaceSchedulesAsync(Guid submissionId, IEnumerable<ParkingSchedule> schedules) => Task.CompletedTask;
}

public class FakeStudentRepository : IStudentRepository
{
    public Task<Student?> GetByUserProfileIdAsync(Guid userProfileId) => Task.FromResult<Student?>(null);
    public Task AddAsync(Student student) => Task.CompletedTask;
    public Task UpdateAsync(Student student) => Task.CompletedTask;
    public Task DeleteAsync(Student student) => Task.CompletedTask;
    public Task<Student?> GetByStudentNumberAsync(string studentNumber, Guid? excludeProfileId = null) => Task.FromResult<Student?>(null);
    public Task<bool> StudentNumberExistsAsync(string studentNumber, Guid? excludeProfileId = null) => Task.FromResult(false);
}

public class FakePersonnelRepository : IPersonnelRepository
{
    public Task<Personnel?> GetByUserProfileIdAsync(Guid userProfileId) => Task.FromResult<Personnel?>(null);
    public Task AddAsync(Personnel personnel) => Task.CompletedTask;
    public Task UpdateAsync(Personnel personnel) => Task.CompletedTask;
    public Task DeleteAsync(Personnel personnel) => Task.CompletedTask;
    public Task<Personnel?> GetByIdCardNumberAsync(string idCardNumber, Guid? excludeProfileId = null) => Task.FromResult<Personnel?>(null);
    public Task<bool> IdCardNumberExistsAsync(string idCardNumber, Guid? excludeProfileId = null) => Task.FromResult(false);
}

public class FakeParkingService : IParkingService
{
    public ParkingLog CreateEntry(Guid vehicleId, Guid? guardId, EntryMethod entryMethod = EntryMethod.QrCode) => null!;
    public void MarkExit(ParkingLog parkingLog) { }
    public DateTime CalculateEstimatedExitTime(DateTime entryTime, TimeSpan scheduleEndTime, int? graceMinutes = null) => default;
    public DateTime CalculateMaximumExitTime(DateTime entryTime, TimeSpan scheduleEndTime, int? graceMinutes = null) => default;
    public double CalculateTotalParkingHours(DateTime entryTime, DateTime exitTime) => 0.0;
}

public class FakeScheduleService : IScheduleService
{
    public bool CanEnter(DateTime currentTime, ParkingSchedule schedule) => true;
    public TimeSpan GetEarliestAllowedEntryTime(ParkingSchedule schedule) => default;
}
