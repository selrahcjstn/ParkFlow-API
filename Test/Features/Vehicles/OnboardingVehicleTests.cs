using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingVehicle;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Auth;

namespace Test.Features.Vehicles;

public class OnboardingVehicleTests
{
    private readonly InMemoryVehicleRepository _vehicleRepository;
    private readonly FakeUserAccountRepository _userAccountRepository;
    private readonly FakeQrCodeService _qrCodeService;
    private readonly UpdateOnboardingVehicleValidator _validator;

    public OnboardingVehicleTests()
    {
        _vehicleRepository = new InMemoryVehicleRepository();
        _userAccountRepository = new FakeUserAccountRepository();
        _qrCodeService = new FakeQrCodeService();
        _validator = new UpdateOnboardingVehicleValidator();
    }

    [Fact]
    public async Task UpdateOnboardingVehicleHandler_ShouldInsertNewVehicle_WhenUserHasNoVehicles()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserAccount("password", "+639999999999");
        var userIdProp = typeof(BaseEntity).GetProperty("Id");
        userIdProp?.SetValue(user, userId);
        await _userAccountRepository.AddAsync(user);

        var handler = new UpdateOnboardingVehicleHandler(_vehicleRepository, _userAccountRepository, _qrCodeService, _validator);
        var command = new UpdateOnboardingVehicleCommand(userId, "ABC-123", "Toyota", VehicleType.Car);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var vehicles = _vehicleRepository.Vehicles.Where(v => v.OwnerId == userId).ToList();
        Assert.Single(vehicles);
        var registered = vehicles.First();
        Assert.Equal("ABC-123", registered.PlateNumber);
        Assert.Equal("Toyota", registered.Brand);
        Assert.True(registered.IsPrimary);
        
        // Assert onboarding step updated
        var updatedUser = await _userAccountRepository.GetByIdAsync(userId);
        Assert.Equal(OnboardingStep.Schedule, updatedUser?.OnboardingStep);
    }

    [Fact]
    public async Task UpdateOnboardingVehicleHandler_ShouldBeIdempotent_WhenResubmittedWithSameDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserAccount("password", "+639999999999");
        var userIdProp = typeof(BaseEntity).GetProperty("Id");
        userIdProp?.SetValue(user, userId);
        await _userAccountRepository.AddAsync(user);

        var handler = new UpdateOnboardingVehicleHandler(_vehicleRepository, _userAccountRepository, _qrCodeService, _validator);
        
        // Initial submission
        var result1 = await handler.Handle(new UpdateOnboardingVehicleCommand(userId, "ABC-123", "Toyota", VehicleType.Car), CancellationToken.None);
        Assert.True(result1.IsSuccess);

        // Act - Resubmit the exact same details (simulating clicking Continue again)
        var result2 = await handler.Handle(new UpdateOnboardingVehicleCommand(userId, "ABC-123", "Toyota", VehicleType.Car), CancellationToken.None);

        // Assert
        Assert.True(result2.IsSuccess);
        var vehicles = _vehicleRepository.Vehicles.Where(v => v.OwnerId == userId).ToList();
        Assert.Single(vehicles); // Still exactly 1 vehicle
        var registered = vehicles.First();
        Assert.Equal("ABC-123", registered.PlateNumber);
        Assert.Equal("Toyota", registered.Brand);
    }

    [Fact]
    public async Task UpdateOnboardingVehicleHandler_ShouldUpdateExistingVehicle_WhenEditedAndResubmitted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserAccount("password", "+639999999999");
        var userIdProp = typeof(BaseEntity).GetProperty("Id");
        userIdProp?.SetValue(user, userId);
        await _userAccountRepository.AddAsync(user);

        var handler = new UpdateOnboardingVehicleHandler(_vehicleRepository, _userAccountRepository, _qrCodeService, _validator);
        
        // Initial submission (Toyota ABC-123)
        var result1 = await handler.Handle(new UpdateOnboardingVehicleCommand(userId, "ABC-123", "Toyota", VehicleType.Car), CancellationToken.None);
        Assert.True(result1.IsSuccess);

        // Act - Edit details to Honda XYZ-789 and resubmit
        var result2 = await handler.Handle(new UpdateOnboardingVehicleCommand(userId, "XYZ-789", "Honda", VehicleType.Motorcycle), CancellationToken.None);

        // Assert
        Assert.True(result2.IsSuccess);
        var vehicles = _vehicleRepository.Vehicles.Where(v => v.OwnerId == userId).ToList();
        Assert.Single(vehicles); // Updates existing, does not create new one
        var registered = vehicles.First();
        Assert.Equal("XYZ-789", registered.PlateNumber);
        Assert.Equal("Honda", registered.Brand);
        Assert.Equal(VehicleType.Motorcycle, registered.VehicleType);
    }

    [Fact]
    public async Task UpdateOnboardingVehicleHandler_ShouldPreventPlateCollision_WithOtherVehiclesOfTheSameUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserAccount("password", "+639999999999");
        var userIdProp = typeof(BaseEntity).GetProperty("Id");
        userIdProp?.SetValue(user, userId);
        await _userAccountRepository.AddAsync(user);

        var handler = new UpdateOnboardingVehicleHandler(_vehicleRepository, _userAccountRepository, _qrCodeService, _validator);

        // Setup: user already has two vehicles (e.g. registered through main profile features, not onboarding)
        var vehicle1 = new Vehicle(userId, "ABC-123", "Toyota", "hash1", VehicleType.Car);
        var vehicle2 = new Vehicle(userId, "XYZ-789", "Honda", "hash2", VehicleType.Car);
        await _vehicleRepository.AddAsync(vehicle1);
        await _vehicleRepository.AddAsync(vehicle2);

        // Act - Try to onboarding-update vehicle1 to use vehicle2's plate number "XYZ-789"
        var result = await handler.Handle(new UpdateOnboardingVehicleCommand(userId, "XYZ-789", "Ford", VehicleType.Car), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Contains("plate number already exists", result.Message);
    }
}
