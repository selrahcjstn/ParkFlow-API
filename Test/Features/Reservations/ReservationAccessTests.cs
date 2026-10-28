using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Features.Reservations.Queries.VerifyReservationScan;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.ParkingLogs;
using Test.Support;
using Xunit;

namespace Test.Features.Reservations;

public class ReservationAccessTests
{
    [Theory]
    [InlineData(ReservationType.Normal)]
    [InlineData(ReservationType.Special)]
    public async Task ReservationPassRetainsOperationalValidation(ReservationType type)
    {
        var reservations = new FakeParkingReservationRepository();
        var vehicles = new FakeVehicleRepository();
        var profiles = new FakeTestUserProfileRepository();
        var ownerId = Guid.NewGuid();
        var vehicle = new Vehicle(ownerId, "ABC123", "Toyota", "qr", VehicleType.Car);
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Verified);
        await vehicles.AddAsync(vehicle);
        var profile = new UserProfile(ownerId, "Driver", "One", null, "profile.jpg");
        profile.SubmitLicense("license.jpg");
        profile.ReviewLicense(true, Guid.NewGuid(), "Approved");
        await profiles.AddAsync(profile);
        var today = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow).Date;
        var reservation = new ParkingReservation(ownerId, "RES-TEST", today, TimeSpan.Zero,
            new TimeSpan(23, 59, 59), "Campus visit", type, vehicle.Id);
        reservation.Approve(Guid.NewGuid());
        await reservations.AddAsync(reservation);
        var handler = new VerifyReservationScanHandler(new FakeParkingLogRepositoryWithActiveLogs([]), reservations, vehicles, profiles);
        var query = new VerifyReservationScanQuery("RES-TEST");

        var allowed = await handler.Handle(query, default);
        Assert.True(allowed.IsSuccess);
        Assert.True(allowed.Data!.IsValid, allowed.Data.StatusMessage);
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Pending);
        var denied = await handler.Handle(query, default);
        Assert.False(denied.Data!.IsValid);
        Assert.Contains("approval", denied.Data.StatusMessage);
    }
}
