using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Reservations.Commands.CreateReservation;
using ParkFlow.Application.Features.Reservations.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace Test.Features.Reservations;

public class FakeParkingReservationRepository : IParkingReservationRepository
{
    public List<ParkingReservation> Reservations { get; } = new();

    public Task AddAsync(ParkingReservation reservation)
    {
        Reservations.Add(reservation);
        return Task.CompletedTask;
    }

    public Task<ParkingReservation?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(Reservations.FirstOrDefault(r => r.Id == id));
    }

    public Task<IEnumerable<ParkingReservation>> GetByUserIdAsync(Guid userId)
    {
        return Task.FromResult<IEnumerable<ParkingReservation>>(Reservations.Where(r => r.UserId == userId).ToList());
    }

    public Task<ParkingReservation?> GetByReferenceNumberAsync(string referenceNumber)
    {
        return Task.FromResult(Reservations.FirstOrDefault(r => r.ReferenceNumber == referenceNumber));
    }

    public Task<IEnumerable<ParkingReservation>> GetAllAsync(ReservationStatus? status = null)
    {
        var q = Reservations.AsEnumerable();
        if (status.HasValue)
            q = q.Where(r => r.Status == status.Value);
        return Task.FromResult<IEnumerable<ParkingReservation>>(q.ToList());
    }

    public Task UpdateAsync(ParkingReservation reservation) => Task.CompletedTask;
    public Task SaveChangesAsync() => Task.CompletedTask;
}

public class FakeUserAccountRepository : IUserAccountRepository
{
    public List<UserAccount> Users { get; } = new();

    public Task<UserAccount?> GetByIdAsync(Guid id) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
    public Task<UserAccount?> GetByEmailAsync(string email) => Task.FromResult(Users.FirstOrDefault());
    public Task<UserAccount?> GetByIdentityAsync(string provider, string providerSubjectId) => Task.FromResult(Users.FirstOrDefault());
    public Task<UserAccount?> GetByStudentFacultyNumberAsync(string studentFacultyNumber) => Task.FromResult(Users.FirstOrDefault());
    public Task<UserAccount?> GetByAuthProviderExternalIdAsync(AuthProvider provider, string externalId) => Task.FromResult(Users.FirstOrDefault());
    public Task<UserAccount?> GetByPhoneNumberAsync(string phoneNumber) => Task.FromResult(Users.FirstOrDefault());
    public Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null) => Task.FromResult(false);
    public Task AddAsync(UserAccount user) { Users.Add(user); return Task.CompletedTask; }
    public Task UpdateAsync(UserAccount user) => Task.CompletedTask;
    public Task DeleteAsync(UserAccount user) => Task.CompletedTask;
    public Task<bool> DeleteAsync(Guid id) => Task.FromResult(true);
    public Task<IEnumerable<UserAccount>> ListAllAsync() => Task.FromResult<IEnumerable<UserAccount>>(Users);
    public Task<UserAccount?> GetByIdWithDetailsAsync(Guid id) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
    public Task<(IEnumerable<UserAccount> Users, int TotalCount)> GetUsersListAsync(int pageNumber, int pageSize, string? searchQuery, string? roleFilter, string? statusFilter)
        => Task.FromResult<(IEnumerable<UserAccount>, int)>((Users, Users.Count));
}

public class FakeVehicleRepository : IVehicleRepository
{
    public List<Vehicle> Vehicles { get; } = new();

    public Task AddAsync(Vehicle vehicle) { Vehicles.Add(vehicle); return Task.CompletedTask; }
    public Task<Vehicle?> GetByIdAsync(Guid id) => Task.FromResult(Vehicles.FirstOrDefault(v => v.Id == id));
    public Task<Vehicle?> GetByQrCodeHashAsync(string qrCodeHash) => Task.FromResult(Vehicles.FirstOrDefault());
    public Task<Vehicle?> GetByPlateNumberAsync(string plateNumber) => Task.FromResult(Vehicles.FirstOrDefault());
    public Task<IEnumerable<Vehicle>> GetByOwnerIdAsync(Guid ownerId) => Task.FromResult<IEnumerable<Vehicle>>(Vehicles.Where(v => v.OwnerId == ownerId).ToList());
    public Task<IEnumerable<Vehicle>> GetByOwnerIdsAsync(IEnumerable<Guid> ownerIds) => Task.FromResult<IEnumerable<Vehicle>>(Vehicles.Where(v => ownerIds.Contains(v.OwnerId)).ToList());
    public Task<Vehicle?> GetPrimaryByOwnerIdAsync(Guid ownerId) => Task.FromResult(Vehicles.FirstOrDefault(v => v.OwnerId == ownerId && v.IsPrimary));
    public Task<IEnumerable<Vehicle>> GetAllAsync() => Task.FromResult<IEnumerable<Vehicle>>(Vehicles);
    public Task UpdateAsync(Vehicle vehicle) => Task.CompletedTask;
    public Task DeleteAsync(Vehicle vehicle) => Task.CompletedTask;
    public Task SaveChangesAsync() => Task.CompletedTask;
}

public class FakeNotificationSender : ISignalRNotificationSender
{
    public Task SendEventNotificationAsync(string userId, object data) => Task.CompletedTask;
    public Task SendToUserAsync(string userId, string method, object data) => Task.CompletedTask;
    public Task SendToAllAsync(string method, object data) => Task.CompletedTask;
}

public class FakeEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string body) => Task.CompletedTask;
    public Task SendTemplateEmailAsync(string to, string templateName, object model) => Task.CompletedTask;
}

public class CreateReservationRestrictionTests
{
    private readonly FakeParkingReservationRepository _reservationRepo = new();
    private readonly FakeUserAccountRepository _userRepo = new();
    private readonly FakeVehicleRepository _vehicleRepo = new();
    private readonly CreateReservationValidator _validator = new();
    private readonly FakeNotificationSender _notifSender = new();
    private readonly FakeEmailService _emailService = new();

    private CreateReservationHandler CreateHandler()
    {
        return new CreateReservationHandler(
            _reservationRepo,
            _userRepo,
            _vehicleRepo,
            _validator,
            _notifSender,
            _emailService
        );
    }

    [Fact]
    public async Task CreateReservation_FirstTimeOnDate_ShouldSucceed()
    {
        var user = new UserAccount("hash", "09123456789");
        user.Verify();
        _userRepo.Users.Add(user);

        var vehicle = new Vehicle(user.Id, "ABC-1234", "Toyota Vios", "qr123", VehicleType.Car);
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
        _vehicleRepo.Vehicles.Add(vehicle);

        var handler = CreateHandler();
        var targetDate = DateTime.UtcNow.Date.AddDays(2);

        var cmd = new CreateReservationCommand(
            user.Id,
            targetDate,
            new TimeSpan(8, 0, 0),
            new TimeSpan(17, 0, 0),
            "Academic Consultation",
            null,
            ReservationType.Normal,
            vehicle.Id
        );

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(_reservationRepo.Reservations);
    }

    [Fact]
    public async Task CreateReservation_SecondReservationOnSameDate_SameVehicle_ShouldFailWithConflict()
    {
        var user = new UserAccount("hash", "09123456789");
        user.Verify();
        _userRepo.Users.Add(user);

        var vehicle = new Vehicle(user.Id, "ABC-1234", "Toyota Vios", "qr123", VehicleType.Car);
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
        _vehicleRepo.Vehicles.Add(vehicle);

        var handler = CreateHandler();
        var targetDate = DateTime.UtcNow.Date.AddDays(3);

        var cmd1 = new CreateReservationCommand(
            user.Id,
            targetDate,
            new TimeSpan(8, 0, 0),
            new TimeSpan(12, 0, 0),
            "First Reservation",
            null,
            ReservationType.Normal,
            vehicle.Id
        );
        var res1 = await handler.Handle(cmd1, CancellationToken.None);
        Assert.True(res1.IsSuccess);

        // Attempt second reservation on same date
        var cmd2 = new CreateReservationCommand(
            user.Id,
            targetDate,
            new TimeSpan(13, 0, 0),
            new TimeSpan(17, 0, 0),
            "Second Reservation Attempt",
            null,
            ReservationType.Normal,
            vehicle.Id
        );
        var res2 = await handler.Handle(cmd2, CancellationToken.None);

        Assert.False(res2.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, res2.ErrorCode);
        Assert.Contains("Only one reservation per day is allowed", res2.Message);
        Assert.Single(_reservationRepo.Reservations);
    }

    [Fact]
    public async Task CreateReservation_SecondReservationOnSameDate_DifferentVehicle_ShouldFailWithConflict()
    {
        var user = new UserAccount("hash", "09123456789");
        user.Verify();
        _userRepo.Users.Add(user);

        var vehicle1 = new Vehicle(user.Id, "ABC-1234", "Toyota Vios", "qr1", VehicleType.Car);
        vehicle1.UpdateVerificationStatus(CorVerificationStatus.Verified);
        _vehicleRepo.Vehicles.Add(vehicle1);

        var vehicle2 = new Vehicle(user.Id, "XYZ-9999", "Honda Click", "qr2", VehicleType.Motorcycle);
        vehicle2.UpdateVerificationStatus(CorVerificationStatus.Verified);
        _vehicleRepo.Vehicles.Add(vehicle2);

        var handler = CreateHandler();
        var targetDate = DateTime.UtcNow.Date.AddDays(4);

        // Create reservation with vehicle1
        var cmd1 = new CreateReservationCommand(
            user.Id,
            targetDate,
            new TimeSpan(8, 0, 0),
            new TimeSpan(12, 0, 0),
            "First Reservation with Car",
            null,
            ReservationType.Normal,
            vehicle1.Id
        );
        var res1 = await handler.Handle(cmd1, CancellationToken.None);
        Assert.True(res1.IsSuccess);

        // Attempt reservation on same date with vehicle2
        var cmd2 = new CreateReservationCommand(
            user.Id,
            targetDate,
            new TimeSpan(13, 0, 0),
            new TimeSpan(17, 0, 0),
            "Second Reservation with Motorcycle",
            null,
            ReservationType.Normal,
            vehicle2.Id
        );
        var res2 = await handler.Handle(cmd2, CancellationToken.None);

        Assert.False(res2.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, res2.ErrorCode);
        Assert.Contains("Only one reservation per day is allowed", res2.Message);
        Assert.Single(_reservationRepo.Reservations);
    }

    [Fact]
    public async Task CreateReservation_DifferentDates_ShouldBothSucceed()
    {
        var user = new UserAccount("hash", "09123456789");
        user.Verify();
        _userRepo.Users.Add(user);

        var vehicle = new Vehicle(user.Id, "ABC-1234", "Toyota Vios", "qr123", VehicleType.Car);
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
        _vehicleRepo.Vehicles.Add(vehicle);

        var handler = CreateHandler();
        var date1 = DateTime.UtcNow.Date.AddDays(2);
        var date2 = DateTime.UtcNow.Date.AddDays(3);

        var cmd1 = new CreateReservationCommand(
            user.Id,
            date1,
            new TimeSpan(8, 0, 0),
            new TimeSpan(12, 0, 0),
            "Day 1 Reservation",
            null,
            ReservationType.Normal,
            vehicle.Id
        );
        var res1 = await handler.Handle(cmd1, CancellationToken.None);
        Assert.True(res1.IsSuccess);

        var cmd2 = new CreateReservationCommand(
            user.Id,
            date2,
            new TimeSpan(8, 0, 0),
            new TimeSpan(12, 0, 0),
            "Day 2 Reservation",
            null,
            ReservationType.Normal,
            vehicle.Id
        );
        var res2 = await handler.Handle(cmd2, CancellationToken.None);
        Assert.True(res2.IsSuccess);

        Assert.Equal(2, _reservationRepo.Reservations.Count);
    }

    [Fact]
    public async Task CreateReservation_AfterPreviousReservationCancelled_ShouldSucceed()
    {
        var user = new UserAccount("hash", "09123456789");
        user.Verify();
        _userRepo.Users.Add(user);

        var vehicle = new Vehicle(user.Id, "ABC-1234", "Toyota Vios", "qr123", VehicleType.Car);
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
        _vehicleRepo.Vehicles.Add(vehicle);

        var handler = CreateHandler();
        var targetDate = DateTime.UtcNow.Date.AddDays(5);

        // First reservation
        var cmd1 = new CreateReservationCommand(
            user.Id,
            targetDate,
            new TimeSpan(8, 0, 0),
            new TimeSpan(12, 0, 0),
            "First Reservation",
            null,
            ReservationType.Normal,
            vehicle.Id
        );
        var res1 = await handler.Handle(cmd1, CancellationToken.None);
        Assert.True(res1.IsSuccess);

        // Cancel it
        var reservation = _reservationRepo.Reservations.First();
        reservation.Cancel();

        // Second reservation on same date now succeeds
        var cmd2 = new CreateReservationCommand(
            user.Id,
            targetDate,
            new TimeSpan(13, 0, 0),
            new TimeSpan(17, 0, 0),
            "New Reservation after cancellation",
            null,
            ReservationType.Normal,
            vehicle.Id
        );
        var res2 = await handler.Handle(cmd2, CancellationToken.None);
        Assert.True(res2.IsSuccess);

        Assert.Equal(2, _reservationRepo.Reservations.Count);
    }
}
