using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Features.Reservations.Queries.VerifyReservationScan;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.ParkingLogs;
using Test.Features.Violations;
using Xunit;

namespace Test.Features.Reservations;

public class ReservationViolationAccessTests
{
    [Theory]
    [InlineData(ReservationType.Normal, 1)]
    [InlineData(ReservationType.Normal, 2)]
    [InlineData(ReservationType.Normal, 3)]
    [InlineData(ReservationType.Special, 1)]
    public async Task ReservationPass_BlocksUnpaidViolations_AndAllowsAfterSettlement(ReservationType type, int unpaidCount)
    {
        var reservations = new FakeParkingReservationRepository();
        var vehicles = new FakeVehicleRepository();
        var profiles = new FakeTestUserProfileRepository();
        var violations = new FakeViolationRepository { ActiveViolationCount = unpaidCount };
        var ownerId = Guid.NewGuid();
        var vehicle = new Vehicle(ownerId, "ABC123", "Toyota", "qr", VehicleType.Car);
        await vehicles.AddAsync(vehicle);
        var today = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow).Date;
        var reservation = new ParkingReservation(ownerId, "RES-TEST", today, TimeSpan.Zero,
            new TimeSpan(23, 59, 59), "Campus visit", type, vehicle.Id);
        reservation.Approve(Guid.NewGuid());
        await reservations.AddAsync(reservation);
        var handler = new VerifyReservationScanHandler(reservations, vehicles, profiles, violations);
        var query = new VerifyReservationScanQuery("RES-TEST");

        var denied = await handler.Handle(query, default);
        Assert.True(denied.IsSuccess);
        Assert.False(denied.Data!.IsValid);
        Assert.Contains("settle all unpaid violations", denied.Data.StatusMessage);

        violations.ActiveViolationCount = 0;
        var allowed = await handler.Handle(query, default);
        Assert.True(allowed.IsSuccess);
        Assert.True(allowed.Data!.IsValid, allowed.Data.StatusMessage);
    }
}
