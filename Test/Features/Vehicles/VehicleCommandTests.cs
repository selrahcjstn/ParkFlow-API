using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Vehicles.Command;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace Test.Features.Vehicles;

public class FakeQrCodeService : IQrCodeService
{
    public byte[] GenerateQrCode(string text) => Array.Empty<byte>();
}

public class FakeParkingLogRepository : IParkingLogRepository
{
    public ParkingLog? ActiveParkingLog { get; set; }
    public Task AddParkingLogAsync(ParkingLog parkingLog) => Task.CompletedTask;
    public Task<ParkingLog?> GetActiveParkingLogByVehicleIdAsync(Guid vehicleId) => Task.FromResult<ParkingLog?>(ActiveParkingLog);
    public Task<IReadOnlyList<ParkingLog>> GetActiveParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetTodaysParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetRecentParkingLogsAsync(int limit) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task<IReadOnlyList<ParkingLog>> GetParkingHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15) => Task.FromResult<IReadOnlyList<ParkingLog>>(new List<ParkingLog>());
    public Task UpdateParkingLogAsync(ParkingLog parkingLog) => Task.CompletedTask;
}

public class InMemoryVehicleRepository : IVehicleRepository
{
    public List<Vehicle> Vehicles { get; } = new();

    public Task AddAsync(Vehicle vehicle)
    {
        Vehicles.Add(vehicle);
        return Task.CompletedTask;
    }

    public Task<Vehicle?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(Vehicles.FirstOrDefault(v => v.Id == id));
    }

    public Task<Vehicle?> GetByQrCodeHashAsync(string qrCodeHash)
    {
        return Task.FromResult(Vehicles.FirstOrDefault(v => v.QrCodeHash == qrCodeHash));
    }

    public Task<Vehicle?> GetByPlateNumberAsync(string plateNumber)
    {
        return Task.FromResult(Vehicles.FirstOrDefault(v => v.PlateNumber.Equals(plateNumber, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<IEnumerable<Vehicle>> GetByOwnerIdAsync(Guid ownerId)
    {
        return Task.FromResult<IEnumerable<Vehicle>>(Vehicles.Where(v => v.OwnerId == ownerId).ToList());
    }

    public Task<IEnumerable<Vehicle>> GetByOwnerIdsAsync(IEnumerable<Guid> ownerIds)
    {
        return Task.FromResult<IEnumerable<Vehicle>>(Vehicles.Where(v => ownerIds.Contains(v.OwnerId)).ToList());
    }

    public Task<IEnumerable<Vehicle>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<Vehicle>>(Vehicles.ToList());
    }

    public Task UpdateAsync(Vehicle vehicle)
    {
        var existing = Vehicles.FirstOrDefault(v => v.Id == vehicle.Id);
        if (existing != null)
        {
            Vehicles.Remove(existing);
            Vehicles.Add(vehicle);
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Vehicle vehicle)
    {
        var existing = Vehicles.FirstOrDefault(v => v.Id == vehicle.Id);
        if (existing != null)
        {
            Vehicles.Remove(existing);
        }
        return Task.CompletedTask;
    }
}

public class VehicleCommandTests
{
    private readonly InMemoryVehicleRepository _repository;
    private readonly FakeQrCodeService _qrCodeService;
    private readonly CreateVehicleValidator _validator;

    public VehicleCommandTests()
    {
        _repository = new InMemoryVehicleRepository();
        _qrCodeService = new FakeQrCodeService();
        _validator = new CreateVehicleValidator();
    }

    [Fact]
    public async Task CreateVehicleHandler_ShouldAutomaticallyMakeFirstVehiclePrimary()
    {
        // Arrange
        var handler = new CreateVehicleHandler(_repository, _validator, _qrCodeService);
        var ownerId = Guid.NewGuid();
        var command = new CreateVehicleCommand(ownerId, "ABC-123", "Toyota", VehicleType.Car);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var vehicles = _repository.Vehicles.Where(v => v.OwnerId == ownerId).ToList();
        Assert.Single(vehicles);
        Assert.True(vehicles.First().IsPrimary);
    }

    [Fact]
    public async Task CreateVehicleHandler_ShouldNotMakeSubsequentVehiclesPrimary()
    {
        // Arrange
        var handler = new CreateVehicleHandler(_repository, _validator, _qrCodeService);
        var ownerId = Guid.NewGuid();

        // Register first
        await handler.Handle(new CreateVehicleCommand(ownerId, "ABC-123", "Toyota", VehicleType.Car), CancellationToken.None);

        // Act - Register second
        var result = await handler.Handle(new CreateVehicleCommand(ownerId, "XYZ-789", "Honda", VehicleType.Car), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var vehicles = _repository.Vehicles.Where(v => v.OwnerId == ownerId).ToList();
        Assert.Equal(2, vehicles.Count);
        Assert.True(vehicles.First(v => v.PlateNumber == "ABC-123").IsPrimary);
        Assert.False(vehicles.First(v => v.PlateNumber == "XYZ-789").IsPrimary);
    }

    [Fact]
    public async Task CreateVehicleHandler_ShouldEnforceMaxFiveVehicles()
    {
        // Arrange
        var handler = new CreateVehicleHandler(_repository, _validator, _qrCodeService);
        var ownerId = Guid.NewGuid();

        // Add 5 vehicles
        for (int i = 1; i <= 5; i++)
        {
            var res = await handler.Handle(new CreateVehicleCommand(ownerId, $"PLATE-{i}", "Toyota", VehicleType.Car), CancellationToken.None);
            Assert.True(res.IsSuccess);
        }

        // Act - Try adding 6th
        var result = await handler.Handle(new CreateVehicleCommand(ownerId, "PLATE-6", "Honda", VehicleType.Car), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Contains("maximum of 5 vehicles", result.Message);
    }

    [Fact]
    public async Task UpdateVehicleHandler_ShouldSuccessfullyUpdateVehicle()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var vehicle = new Vehicle(ownerId, "ABC-123", "Toyota", "hash", VehicleType.Car);
        vehicle.SetPrimary(true);
        await _repository.AddAsync(vehicle);

        var handler = new UpdateVehicleHandler(_repository);
        var command = new UpdateVehicleCommand(vehicle.Id, ownerId, "XYZ-999", "Honda", VehicleType.Motorcycle);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var updated = await _repository.GetByIdAsync(vehicle.Id);
        Assert.NotNull(updated);
        Assert.Equal("XYZ-999", updated.PlateNumber);
        Assert.Equal("Honda", updated.Brand);
        Assert.Equal(VehicleType.Motorcycle, updated.VehicleType);
        Assert.True(updated.IsPrimary);
    }

    [Fact]
    public async Task UpdateVehicleHandler_ShouldPreventPlateNumberCollision()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var vehicle1 = new Vehicle(ownerId, "ABC-123", "Toyota", "hash1", VehicleType.Car);
        var vehicle2 = new Vehicle(ownerId, "XYZ-789", "Honda", "hash2", VehicleType.Car);
        await _repository.AddAsync(vehicle1);
        await _repository.AddAsync(vehicle2);

        var handler = new UpdateVehicleHandler(_repository);
        var command = new UpdateVehicleCommand(vehicle2.Id, ownerId, "ABC-123", "Honda", VehicleType.Car);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Contains("plate number already exists", result.Message);
    }

    [Fact]
    public async Task DeleteVehicleHandler_ShouldSuccessfullyDeleteVehicle()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var vehicle = new Vehicle(ownerId, "ABC-123", "Toyota", "hash", VehicleType.Car);
        await _repository.AddAsync(vehicle);

        var handler = new DeleteVehicleHandler(_repository, new FakeParkingLogRepository());
        var command = new DeleteVehicleCommand(vehicle.Id, ownerId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var deleted = await _repository.GetByIdAsync(vehicle.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteVehicleHandler_ShouldReturnBadRequestIfVehicleIsPrimary()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var vehicle = new Vehicle(ownerId, "ABC-123", "Toyota", "hash1", VehicleType.Car);
        vehicle.SetPrimary(true);
        await _repository.AddAsync(vehicle);

        var handler = new DeleteVehicleHandler(_repository, new FakeParkingLogRepository());
        var command = new DeleteVehicleCommand(vehicle.Id, ownerId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
        Assert.Contains("Cannot delete the primary vehicle", result.Message);
        
        var notDeleted = await _repository.GetByIdAsync(vehicle.Id);
        Assert.NotNull(notDeleted);
    }

    [Fact]
    public async Task DeleteVehicleHandler_ShouldReturnBadRequestIfVehicleHasActiveParkingSession()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var vehicle = new Vehicle(ownerId, "ABC-123", "Toyota", "hash1", VehicleType.Car);
        await _repository.AddAsync(vehicle);

        var fakeParkingLogRepo = new FakeParkingLogRepository();
        fakeParkingLogRepo.ActiveParkingLog = new ParkingLog(vehicle.Id, Guid.NewGuid(), ParkingStatus.Parked);

        var handler = new DeleteVehicleHandler(_repository, fakeParkingLogRepo);
        var command = new DeleteVehicleCommand(vehicle.Id, ownerId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
        Assert.Contains("Cannot delete a vehicle with an active parking session", result.Message);

        var notDeleted = await _repository.GetByIdAsync(vehicle.Id);
        Assert.NotNull(notDeleted);
    }

    [Fact]
    public async Task GetVehiclesByOwnerIdHandler_ShouldReturnIsPrimary()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var vehicle1 = new Vehicle(ownerId, "ABC-123", "Toyota", "hash1", VehicleType.Car);
        vehicle1.SetPrimary(true);
        var vehicle2 = new Vehicle(ownerId, "XYZ-789", "Honda", "hash2", VehicleType.Car);
        vehicle2.SetPrimary(false);
        await _repository.AddAsync(vehicle1);
        await _repository.AddAsync(vehicle2);

        var parkingLogRepository = new FakeParkingLogRepository();
        var handler = new ParkFlow.Application.Features.Vehicles.Queries.GetVehiclesByOwnerId.GetVehiclesByOwnerIdHandler(_repository, parkingLogRepository);

        // Act
        var result = await handler.Handle(new ParkFlow.Application.Features.Vehicles.Queries.GetVehiclesByOwnerId.GetVehiclesByOwnerIdQuery(ownerId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        var dtoList = result.Data!.ToList();
        Assert.Equal(2, dtoList.Count);
        
        var v1Dto = dtoList.First(d => d.Id == vehicle1.Id);
        var v2Dto = dtoList.First(d => d.Id == vehicle2.Id);
        
        Assert.True(v1Dto.IsPrimary);
        Assert.False(v2Dto.IsPrimary);
    }

    [Fact]
    public async Task SetPrimaryVehicleHandler_ShouldSuccessfullySetPrimaryAndResetOthers()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var vehicle1 = new Vehicle(ownerId, "ABC-123", "Toyota", "hash1", VehicleType.Car);
        vehicle1.SetPrimary(true);
        var vehicle2 = new Vehicle(ownerId, "XYZ-789", "Honda", "hash2", VehicleType.Car);
        vehicle2.SetPrimary(false);
        await _repository.AddAsync(vehicle1);
        await _repository.AddAsync(vehicle2);

        var handler = new SetPrimaryVehicleHandler(_repository);
        var command = new SetPrimaryVehicleCommand(vehicle2.Id, ownerId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var v1 = await _repository.GetByIdAsync(vehicle1.Id);
        var v2 = await _repository.GetByIdAsync(vehicle2.Id);
        Assert.NotNull(v1);
        Assert.NotNull(v2);
        Assert.False(v1.IsPrimary);
        Assert.True(v2.IsPrimary);
    }

    [Fact]
    public async Task SetPrimaryVehicleHandler_ShouldReturnNotFoundIfVehicleDoesNotExist()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var handler = new SetPrimaryVehicleHandler(_repository);
        var command = new SetPrimaryVehicleCommand(Guid.NewGuid(), ownerId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SetPrimaryVehicleHandler_ShouldReturnForbiddenIfOwnerDoesNotOwnVehicle()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var vehicle = new Vehicle(otherOwnerId, "ABC-123", "Toyota", "hash", VehicleType.Car);
        await _repository.AddAsync(vehicle);

        var handler = new SetPrimaryVehicleHandler(_repository);
        var command = new SetPrimaryVehicleCommand(vehicle.Id, ownerId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task SetPrimaryVehicleHandler_ShouldReturnBadRequestIfVehicleIsAlreadyPrimary()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var vehicle = new Vehicle(ownerId, "ABC-123", "Toyota", "hash", VehicleType.Car);
        vehicle.SetPrimary(true);
        await _repository.AddAsync(vehicle);

        var handler = new SetPrimaryVehicleHandler(_repository);
        var command = new SetPrimaryVehicleCommand(vehicle.Id, ownerId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
        Assert.Contains("Vehicle is already the primary vehicle", result.Message);
    }
}

