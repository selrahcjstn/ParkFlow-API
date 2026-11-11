using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateManualParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Queries;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Auth;
using Test.Support;

namespace Test.Features.ParkingLogs;

// Fees are configured as zero in production, so these tests prove a violation blocks entry on its own.
public class UnsettledViolationEntryTests
{
    private sealed class FakeViolationRepository(int activeCount) : IViolationRepository
    {
        public Task<int> GetActiveViolationCountAsync(Guid vehicleId, Guid? userId = null) => Task.FromResult(activeCount);
        public Task<int> GetActiveViolationCountByUserIdAsync(Guid userId) => Task.FromResult(activeCount);
        public Task<bool> HasActiveViolationAsync(Guid vehicleId) => Task.FromResult(activeCount > 0);
        public Task<bool> HasActiveViolationByUserIdAsync(Guid userId) => Task.FromResult(activeCount > 0);
        public Task AddAsync(Violation violation) => throw new NotSupportedException();
        public Task<Violation?> GetByIdAsync(Guid id) => throw new NotSupportedException();
        public Task<Violation?> GetByLogIdAsync(Guid logId) => throw new NotSupportedException();
        public Task<Violation?> GetByReferenceNumberAsync(string referenceNumber) => throw new NotSupportedException();
        public Task<IReadOnlyList<Violation>> GetRecentViolationsAsync(int limit) => throw new NotSupportedException();
        public Task<IReadOnlyList<Violation>> GetViolationHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15, bool? unpaidOnly = null) => throw new NotSupportedException();
        public Task<IReadOnlyList<Violation>> GetViolationsByUserIdAsync(Guid userId) => throw new NotSupportedException();
        public Task<Violation?> GetLatestUnsettledByPlateNumberAsync(string plateNumber) => throw new NotSupportedException();
        public Task UpdateAsync(Violation violation) => throw new NotSupportedException();
        public Task DeleteAsync(Violation violation) => throw new NotSupportedException();
    }

    private static (Vehicle vehicle, UserProfile driver) Driver(Guid ownerId)
    {
        var vehicle = new Vehicle(ownerId, "VIO-1234", "Toyota", "VIOLATION-QR", VehicleType.Car,
            verificationStatus: CorVerificationStatus.Verified);
        typeof(BaseEntity).GetProperty("Id")?.SetValue(vehicle, Guid.NewGuid());
        var driver = new UserProfile(ownerId, "Juan", "Dela Cruz", null, null);
        DriverDocumentFixture.Approve(driver);
        driver.UserAccount = new UserAccount(string.Empty, "+639171234567");
        return (vehicle, driver);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(0, true)]
    public async Task QrPreview_FlagsUnsettledViolationBeforeEntry(int activeViolations, bool canEnter)
    {
        var ownerId = Guid.NewGuid();
        var (vehicle, driver) = Driver(ownerId);
        var vehicles = new FakeVehicleRepository();
        await vehicles.AddAsync(vehicle);
        var profiles = new FakeUserProfileRepository();
        await profiles.AddAsync(driver);

        var handler = new PreviewVehicleScanHandler(vehicles, profiles, new Test.Support.FakeParkingLogRepository(),
            new FakeTestParkingReservationRepository(), new FakeViolationRepository(activeViolations));
        var result = await handler.Handle(new PreviewVehicleScan(vehicle.QrCodeHash), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(canEnter, result.Data!.CanEnter);
        Assert.Equal(canEnter ? null : DriverEntryPolicy.UnsettledViolationMessage, result.Data.EntryRestriction);
    }

    [Fact]
    public async Task QrEntry_IsRejectedWhenDriverHasUnsettledViolation()
    {
        var ownerId = Guid.NewGuid();
        var guardId = Guid.NewGuid();
        var (vehicle, driver) = Driver(ownerId);
        var vehicles = new FakeVehicleRepository();
        await vehicles.AddAsync(vehicle);
        var profiles = new FakeUserProfileRepository();
        await profiles.AddAsync(driver);
        var guardProfile = new UserProfile(guardId, "Guard", "One", null, null);
        await profiles.AddAsync(guardProfile);
        var logs = new Test.Support.FakeParkingLogRepository();

        var handler = new CreateParkingLogHandler(logs, vehicles, profiles,
            new FakeGuardRepository { Guard = new Guard(guardProfile, 1) },
            new FakeCorSubmissionRepositoryWithMock([]), new FakeParkingScheduleRepositoryWithMock([]),
            new FakeStudentRepository(), new Test.Features.Users.FakePersonnelRepository(), new Test.Support.FakeAdminRepository(),
            new ParkingService(), new ScheduleService(), new ParkingLogRoleService(), new FakeSignalRNotificationSender(),
            violationRepository: new FakeViolationRepository(1));

        var result = await handler.Handle(new CreateParkingLogCommand(vehicle.QrCodeHash, guardId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal(DriverEntryPolicy.UnsettledViolationMessage, result.Message);
        Assert.Null(logs.ActiveParkingLog);
    }

    [Fact]
    public async Task ManualEntry_IsRejectedWhenDriverHasUnsettledViolation()
    {
        var ownerId = Guid.NewGuid();
        var guardUserId = Guid.NewGuid();
        var (vehicle, driver) = Driver(ownerId);
        var vehicles = new FakeVehicleRepository();
        await vehicles.AddAsync(vehicle);
        var profiles = new FakeUserProfileRepository();
        profiles.Profiles.Add(driver);
        var guardProfile = new UserProfile(guardUserId, "Guard", "One", null, null);
        guardProfile.UserAccount = new UserAccount(string.Empty, "+639888888888");
        typeof(BaseEntity).GetProperty("Id")?.SetValue(guardProfile, Guid.NewGuid());
        profiles.Profiles.Add(guardProfile);
        var logs = new FakeParkingLogRepository();

        var handler = new CreateManualParkingLogHandler(logs, vehicles, profiles, new FakeUserAccountRepository(),
            new FakeGuardRepository { Guard = new Guard(guardProfile, 1) },
            new FakeCorSubmissionRepositoryWithMock([]), new FakeParkingScheduleRepositoryWithMock([]),
            new FakeStudentRepository(), new FakePersonnelRepository(), new FakeAdminRepository(),
            new FakeParkingServiceWithRealEntry(), new FakeScheduleService(), new ParkingLogRoleService(),
            new FakeSignalRNotificationSender(), violationRepository: new FakeViolationRepository(1));

        var result = await handler.Handle(
            new CreateManualParkingLogCommand("VIO-1234", VehicleType.Car, "+639171234567", "Toyota", guardUserId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Equal(DriverEntryPolicy.UnsettledViolationMessage, result.Message);
    }
}
