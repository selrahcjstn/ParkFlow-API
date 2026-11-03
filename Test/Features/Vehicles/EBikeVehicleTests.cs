using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.ExitParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Queries;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Features.Vehicles.Command;
using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Auth;
using Test.Features.ParkingLogs;
using Test.Support;

namespace Test.Features.Vehicles;

public class TestQrCodeService : IQrCodeService
{
    public string? LastPayload { get; private set; }
    public byte[] GenerateQrCode(string text)
    {
        LastPayload = text;
        return System.Text.Encoding.UTF8.GetBytes(text);
    }
}

public class EBikeVehicleTests
{
    private readonly InMemoryVehicleRepository _vehicleRepo;
    private readonly TestQrCodeService _qrCodeService;
    private readonly CreateVehicleValidator _validator;

    public EBikeVehicleTests()
    {
        _vehicleRepo = new InMemoryVehicleRepository();
        _qrCodeService = new TestQrCodeService();
        _validator = new CreateVehicleValidator();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Scenario1_MotorcycleWithoutPlateNumber_FailsValidation(string? plate)
    {
        // Scenario 1: Motorcycle without a plate number fails validation
        var command = new CreateVehicleCommand(Guid.NewGuid(), plate, "Yamaha", VehicleType.Motorcycle,
            VehiclePictureUrl: "https://example.com/motorcycle.jpg");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateVehicleCommand.PlateNumber) &&
                                            e.ErrorMessage.Contains("Plate number is required"));
    }

    [Fact]
    public async Task Scenario2_MotorcycleWithValidPlateNumber_Succeeds()
    {
        // Scenario 2: Motorcycle with a valid plate number succeeds
        var ownerId = Guid.NewGuid();
        var command = new CreateVehicleCommand(ownerId, "MC-1234", "Yamaha", VehicleType.Motorcycle,
            VehiclePictureUrl: "https://example.com/motorcycle.jpg");

        var validationResult = _validator.Validate(command);
        Assert.True(validationResult.IsValid);

        var handler = new CreateVehicleHandler(_vehicleRepo, _validator, _qrCodeService);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var vehicle = await _vehicleRepo.GetByIdAsync(result.Data);
        Assert.NotNull(vehicle);
        Assert.Equal("MC-1234", vehicle.PlateNumber);
        Assert.Equal(VehicleType.Motorcycle, vehicle.VehicleType);
        Assert.True(vehicle.IsPrimary);
        Assert.NotEmpty(vehicle.QrCodeHash);
        Assert.Equal($"{ownerId}:MC-1234:Yamaha", _qrCodeService.LastPayload);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Scenario3_CarWithoutPlateNumber_FailsValidation(string? plate)
    {
        // Scenario 3: Car without a plate number fails validation
        var command = new CreateVehicleCommand(Guid.NewGuid(), plate, "Toyota", VehicleType.Car,
            VehiclePictureUrl: "https://example.com/car.jpg");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateVehicleCommand.PlateNumber) &&
                                            e.ErrorMessage.Contains("Plate number is required"));
    }

    [Fact]
    public async Task Scenario4_CarWithValidPlateNumber_Succeeds()
    {
        // Scenario 4: Car with a valid plate number succeeds
        var ownerId = Guid.NewGuid();
        var command = new CreateVehicleCommand(ownerId, "CAR-5678", "Toyota", VehicleType.Car,
            VehiclePictureUrl: "https://example.com/car.jpg");

        var validationResult = _validator.Validate(command);
        Assert.True(validationResult.IsValid);

        var handler = new CreateVehicleHandler(_vehicleRepo, _validator, _qrCodeService);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var vehicle = await _vehicleRepo.GetByIdAsync(result.Data);
        Assert.NotNull(vehicle);
        Assert.Equal("CAR-5678", vehicle.PlateNumber);
        Assert.Equal(VehicleType.Car, vehicle.VehicleType);
        Assert.True(vehicle.IsPrimary);
        Assert.NotEmpty(vehicle.QrCodeHash);
        Assert.Equal($"{ownerId}:CAR-5678:Toyota", _qrCodeService.LastPayload);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Scenario5_EBikeWithoutPhoto_FailsValidation(string? photoUrl)
    {
        // Scenario 5: E-bike without a photo fails validation (VehiclePictureUrl is mandatory for E-bikes)
        var command = new CreateVehicleCommand(Guid.NewGuid(), null, "TailG", VehicleType.ElectricBike,
            VehiclePictureUrl: photoUrl);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateVehicleCommand.VehiclePictureUrl) &&
                                            e.ErrorMessage.Contains("Vehicle photo is required for E-bike registration"));
    }

    [Fact]
    public async Task Scenario6_EBikeWithPhotoAndNoPlateNumber_Succeeds()
    {
        // Scenario 6: E-bike with photo and no plate number succeeds
        var ownerId = Guid.NewGuid();
        var photoUrl = "https://example.com/ebike-photo.jpg";
        var command = new CreateVehicleCommand(ownerId, null, "TailG", VehicleType.ElectricBike,
            VehiclePictureUrl: photoUrl);

        var validationResult = _validator.Validate(command);
        Assert.True(validationResult.IsValid);

        var handler = new CreateVehicleHandler(_vehicleRepo, _validator, _qrCodeService);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var vehicle = await _vehicleRepo.GetByIdAsync(result.Data);
        Assert.NotNull(vehicle);
        Assert.Null(vehicle.PlateNumber); // No fake or placeholder plate number
        Assert.Equal("TailG", vehicle.Brand);
        Assert.Equal(VehicleType.ElectricBike, vehicle.VehicleType);
        Assert.Equal(photoUrl, vehicle.VehiclePictureUrl);
        Assert.True(vehicle.IsPrimary);
        Assert.NotEmpty(vehicle.QrCodeHash);
        Assert.Equal($"EBIKE:{vehicle.Id}:{ownerId}:TailG", _qrCodeService.LastPayload);
    }

    [Fact]
    public async Task Scenario7_EBikeQrScanning_RetrievesVehicleViaHashOrDirectGuid()
    {
        // Scenario 7: Scanning an e-bike QR code retrieves registered vehicle info via hash or direct GUID
        var ownerId = Guid.NewGuid();
        var vehicle = new Vehicle(ownerId, null, "TailG", "EBIKE-TEMP-HASH", VehicleType.ElectricBike,
            vehiclePictureUrl: "https://example.com/ebike.jpg");
        vehicle.Update(null, "TailG", VehicleType.ElectricBike, $"EBIKE:{vehicle.Id}:{ownerId}:TailG");
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
        await _vehicleRepo.AddAsync(vehicle);

        var profiles = new FakeUserProfileRepository();
        var profile = new UserProfile(ownerId, "Juan", "Dela Cruz", null, null);
        DriverDocumentFixture.Approve(profile);
        profile.UserAccount = new UserAccount("hash", "+639123456789");
        await profiles.AddAsync(profile);

        var logs = new Test.Support.FakeParkingLogRepository();
        var reservations = new FakeTestParkingReservationRepository();
        var handler = new PreviewVehicleScanHandler(_vehicleRepo, profiles, logs, reservations);

        // 1. Scan via QR code hash
        var resultByHash = await handler.Handle(new PreviewVehicleScan(vehicle.QrCodeHash), CancellationToken.None);
        Assert.True(resultByHash.IsSuccess);
        Assert.NotNull(resultByHash.Data);
        Assert.Equal("TailG", resultByHash.Data.Brand);
        Assert.Equal("ElectricBike", resultByHash.Data.VehicleType);
        Assert.Null(resultByHash.Data.PlateNumber);
        Assert.Equal("https://example.com/ebike.jpg", resultByHash.Data.VehiclePictureUrl);
        Assert.Equal("Juan Dela Cruz", resultByHash.Data.FullName);
        Assert.True(resultByHash.Data.CanEnter);

        // 2. Scan via Direct Vehicle ID GUID
        var resultById = await handler.Handle(new PreviewVehicleScan(vehicle.Id.ToString()), CancellationToken.None);
        Assert.True(resultById.IsSuccess);
        Assert.NotNull(resultById.Data);
        Assert.Equal("TailG", resultById.Data.Brand);
        Assert.Equal("ElectricBike", resultById.Data.VehicleType);
        Assert.Null(resultById.Data.PlateNumber);
    }

    [Fact]
    public async Task Scenario8_EBikeEntryAndExit_SucceedWithNullPlate()
    {
        // Scenario 8: E-bike entry and exit workflows succeed with null plate
        var ownerId = Guid.NewGuid();
        var guardId = Guid.NewGuid();

        var vehicle = new Vehicle(ownerId, null, "TailG", "EBIKE-HASH", VehicleType.ElectricBike,
            vehiclePictureUrl: "https://example.com/ebike.jpg");
        vehicle.Update(null, "TailG", VehicleType.ElectricBike, $"EBIKE:{vehicle.Id}:{ownerId}:TailG");
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);

        var vehicles = new FakeVehicleRepository();
        await vehicles.AddAsync(vehicle);

        var profiles = new FakeUserProfileRepository();
        var driverProfile = new UserProfile(ownerId, "Maria", "Santos", null, null);
        DriverDocumentFixture.Approve(driverProfile);
        driverProfile.UserAccount = new UserAccount("hash", "+639123456789");
        await profiles.AddAsync(driverProfile);

        var guardProfile = new UserProfile(guardId, "Guard", "One", null, null);
        await profiles.AddAsync(guardProfile);
        var guards = new FakeGuardRepository { Guard = new Guard(guardProfile, 1) };

        var personnel = new Test.Features.Users.FakePersonnelRepository();
        await personnel.AddAsync(new Personnel(driverProfile.Id, "EMP-EB1", "Engineering", Roles.UniversityStaff));

        var documents = new FakeCorSubmissionRepositoryWithMock([
            new CorSubmission(ownerId, "Employee ID", "https://example.test/id.pdf", verificationStatus: CorVerificationStatus.Verified)
        ]);
        var schedules = new FakeParkingScheduleRepositoryWithMock([]);
        var logs = new Test.Support.FakeParkingLogRepository();
        var students = new FakeStudentRepository();
        var admins = new Test.Support.FakeAdminRepository();
        var parkingService = new ParkingService();
        var scheduleService = new ScheduleService();
        var roles = new ParkingLogRoleService();
        var notifications = new FakeSignalRNotificationSender();

        // Entry via QR code hash
        var entryHandler = new CreateParkingLogHandler(logs, vehicles, profiles, guards, documents, schedules,
            students, personnel, admins, parkingService, scheduleService, roles, notifications);

        var entryResult = await entryHandler.Handle(new CreateParkingLogCommand(vehicle.QrCodeHash, guardId), CancellationToken.None);
        Assert.True(entryResult.IsSuccess);
        Assert.NotNull(entryResult.Data);
        Assert.Null(entryResult.Data.PlateNumber);
        Assert.NotNull(logs.ActiveParkingLog);
        Assert.Equal(vehicle.Id, logs.ActiveParkingLog.VehicleId);

        // Exit via QR code hash
        var exitHandler = new ExitParkingLogHandler(logs, vehicles, profiles, guards, documents, schedules,
            students, personnel, admins, parkingService, roles, new ExitParkingLogValidator(),
            notifications, new FakeTestParkingReservationRepository());

        var exitResult = await exitHandler.Handle(new ExitParkingLogCommand(vehicle.QrCodeHash, guardId), CancellationToken.None);
        Assert.True(exitResult.IsSuccess);
        Assert.NotNull(exitResult.Data);
        Assert.Null(exitResult.Data.PlateNumber);
        Assert.Null(logs.ActiveParkingLog); // Parking session concluded
    }

    [Fact]
    public void Scenario9_AdminVerificationApproval_WorksOnEBikeWithoutPlateNumber()
    {
        // Scenario 9: Admin verification/approval works on E-bike without plate number
        var ebikeSubject = new VerificationSubject(
            Id: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Type: VerificationType.Vehicle,
            Fingerprint: "fingerprint-ebike",
            EmailVerified: true,
            DuplicateIdentifier: false,
            Identifier: null,
            FirstName: "Pedro",
            LastName: "Penduko",
            Course: "IT",
            IsPersonnel: false,
            DocumentUrl: "https://example.com/ebike-photo.jpg",
            AcademicTerm: "2026-2027",
            PlateNumber: null, // No plate number for E-bike
            Brand: "TailG",
            OwnershipDocumentsPresent: true,
            CorApproved: true,
            Schedules: [])
        {
            Suspended = false,
            VehicleType = VehicleType.ElectricBike
        };

        var policy = new VerificationPolicy();

        // E-bike with valid brand, ownership documents and no plate number produces no issues
        var issues = VerificationService.MandatoryChecks(ebikeSubject, policy);
        Assert.Empty(issues);

        // However, if ownership documents (photo, license, profile picture) are missing, E-bike requires them
        var missingDocsSubject = ebikeSubject with { OwnershipDocumentsPresent = false };
        var missingIssues = VerificationService.MandatoryChecks(missingDocsSubject, policy);
        Assert.Contains(missingIssues, i => i.Contains("Upload vehicle photo, driver’s license and profile photo for review"));
        Assert.DoesNotContain(missingIssues, i => i.Contains("OR/CR")); // OR/CR is not demanded for E-bike
    }

    [Fact]
    public async Task Scenario10_MotorcycleAndCarRegistrations_RetainPlateUniqueness_WhileEBikesDoNotConflict()
    {
        // Scenario 10: Existing motorcycle and car registrations continue to work and retain uniqueness constraints,
        // while multiple e-bikes with null plates do not conflict with each other.
        var owner1 = Guid.NewGuid();
        var owner2 = Guid.NewGuid();
        var handler = new CreateVehicleHandler(_vehicleRepo, _validator, _qrCodeService);

        // 1. Car registered with PLATE-101
        var car1 = await handler.Handle(new CreateVehicleCommand(owner1, "PLATE-101", "Toyota", VehicleType.Car), CancellationToken.None);
        Assert.True(car1.IsSuccess);

        // 2. Second car with same plate fails (Conflict)
        var car2 = await handler.Handle(new CreateVehicleCommand(owner2, "PLATE-101", "Honda", VehicleType.Car), CancellationToken.None);
        Assert.False(car2.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, car2.ErrorCode);
        Assert.Contains("already registered", car2.Message);

        // 3. Registering motorcycle with different plate succeeds
        var moto1 = await handler.Handle(new CreateVehicleCommand(owner1, "MOTO-202", "Yamaha", VehicleType.Motorcycle), CancellationToken.None);
        Assert.True(moto1.IsSuccess);

        // 4. Registering e-bike without plate number for owner1 succeeds
        var ebike1 = await handler.Handle(new CreateVehicleCommand(owner1, null, "TailG", VehicleType.ElectricBike,
            VehiclePictureUrl: "https://example.com/tailg.jpg"), CancellationToken.None);
        Assert.True(ebike1.IsSuccess);

        // 5. Registering another e-bike without plate number for owner2 succeeds without plate collision
        var ebike2 = await handler.Handle(new CreateVehicleCommand(owner2, null, "Niu", VehicleType.ElectricBike,
            VehiclePictureUrl: "https://example.com/niu.jpg"), CancellationToken.None);
        Assert.True(ebike2.IsSuccess);

        // 6. Same owner cannot register duplicate e-bike with identical brand
        var duplicateBrandEbike = await handler.Handle(new CreateVehicleCommand(owner1, null, "TailG", VehicleType.ElectricBike,
            VehiclePictureUrl: "https://example.com/tailg-2.jpg"), CancellationToken.None);
        Assert.False(duplicateBrandEbike.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, duplicateBrandEbike.ErrorCode);
        Assert.Contains("already registered", duplicateBrandEbike.Message);
    }
}
