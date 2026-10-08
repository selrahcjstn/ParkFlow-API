using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Reservations;
using ParkFlow.Application.Features.Reservations.Commands.CreateReservation;
using ParkFlow.Application.Features.Reservations.Commands.ApproveReservation;
using ParkFlow.Application.Features.Reservations.Queries.GetReservationAvailability;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Xunit;

namespace Test.Features.Reservations;

public class ReservationCapacityTests
{
    private static readonly DateTime Day = new(2030, 1, 2, 0, 0, 0, DateTimeKind.Utc);
    private static ParkingReservation Booking(int start = 10, int end = 15, ReservationType type = ReservationType.Normal) =>
        new(Guid.NewGuid(), $"RES-{Guid.NewGuid()}", Day, TimeSpan.FromHours(start), TimeSpan.FromHours(end), "Campus visit", type);

    [Theory]
    [InlineData(300, 30, 90)]
    [InlineData(300, 50, 150)]
    [InlineData(31, 30, 9)]
    [InlineData(300, 0, 0)]
    [InlineData(300, 100, 300)]
    public void AllocationIsPartOfTotalCapacity_NotExtraSpaces(int total, int percent, int expected)
    {
        var settings = new SystemSettingsDto { TotalCapacity = total, ReservationAllocationPercent = percent };
        var result = ReservationCapacityPolicy.Describe(settings, 3);
        Assert.Equal(total, result.TotalCapacity);
        Assert.Equal(expected, result.ReservationCapacity);
        Assert.Equal(Math.Max(0, expected - 3), result.AvailableSlots);
        Assert.InRange(result.ReservationCapacity, 0, total);
    }

    [Fact]
    public void NonOverlappingBookingsReuseSpace_AndBoundariesDoNotOverlap()
    {
        ReservationTimeWindow[] windows = [new(TimeSpan.FromHours(10), TimeSpan.FromHours(11)),
            new(TimeSpan.FromHours(11), TimeSpan.FromHours(12)), new(TimeSpan.FromHours(12), TimeSpan.FromHours(13))];
        Assert.Equal(1, ReservationCapacityPolicy.GetPeak(windows, TimeSpan.FromHours(10), TimeSpan.FromHours(15)));
        Assert.Equal(0, ReservationCapacityPolicy.GetPeak(windows, TimeSpan.FromHours(13), TimeSpan.FromHours(15)));
        Assert.Equal(1, ReservationCapacityPolicy.GetPeak(windows, TimeSpan.FromHours(10.5), TimeSpan.FromHours(12.5)));
        Assert.Equal(2, ReservationCapacityPolicy.GetPeak(windows.Append(new(TimeSpan.FromHours(10.5), TimeSpan.FromHours(12.5))), TimeSpan.FromHours(10), TimeSpan.FromHours(15)));
    }

    [Fact]
    public async Task PendingAndApprovedHoldSpaces_CancelledRejectedCompletedAndOtherDatesDoNot()
    {
        var repo = new FakeParkingReservationRepository();
        var pending = Booking(); var approved = Booking(); approved.Approve(Guid.NewGuid());
        var cancelled = Booking(); cancelled.Cancel();
        var rejected = Booking(); rejected.Reject(Guid.NewGuid());
        var completed = Booking(); completed.MarkCompleted();
        var differentDay = new ParkingReservation(Guid.NewGuid(), "OTHER-DAY", Day.AddDays(1), TimeSpan.Zero, TimeSpan.FromHours(23), "Visit");
        repo.Reservations.AddRange([pending, approved, cancelled, rejected, completed, differentDay]);
        Assert.Equal(2, await repo.GetReservedPeakAsync(Day, TimeSpan.FromHours(11), TimeSpan.FromHours(12)));
        var query = new GetReservationAvailabilityHandler(repo);
        var result = await query.Handle(new(Day, TimeSpan.FromHours(11), TimeSpan.FromHours(12)), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.BookedSlots);
        Assert.Equal(result.Data.ReservationCapacity - 2, result.Data.AvailableSlots);
    }

    [Fact]
    public async Task SpecialPassHoldsSpaceForFullDay()
    {
        var repo = new FakeParkingReservationRepository();
        repo.Reservations.Add(Booking(12, 15, ReservationType.Special));
        Assert.Equal(1, await repo.GetReservedPeakAsync(Day, TimeSpan.FromHours(8), TimeSpan.FromHours(9)));
        Assert.Equal(ReservationBookingResult.CapacityFull, await repo.TryAddWithinCapacityAsync(Booking(8, 9), 1));
    }

    [Fact]
    public async Task FullWindowRejectsBooking_AnotherWindowAndReleasedSpaceCanBeBooked()
    {
        var repo = new FakeParkingReservationRepository();
        var first = Booking();
        Assert.Equal(ReservationBookingResult.Created, await repo.TryAddWithinCapacityAsync(first, 1));
        Assert.Equal(ReservationBookingResult.CapacityFull, await repo.TryAddWithinCapacityAsync(Booking(), 1));
        Assert.Equal(ReservationBookingResult.Created, await repo.TryAddWithinCapacityAsync(Booking(15, 16), 1));
        first.Cancel();
        Assert.Equal(ReservationBookingResult.Created, await repo.TryAddWithinCapacityAsync(Booking(), 1));
        Assert.Equal(ReservationBookingResult.CapacityFull, await repo.TryAddWithinCapacityAsync(Booking(17, 18), 0));
    }

    [Fact]
    public async Task LastSlotIsClaimedOnlyOnce_ByConcurrentBookingRequests()
    {
        var repo = new FakeParkingReservationRepository();
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => repo.TryAddWithinCapacityAsync(Booking(), 1)));
        Assert.Single(results, r => r == ReservationBookingResult.Created);
        Assert.Equal(11, results.Count(r => r == ReservationBookingResult.CapacityFull));
        Assert.Single(repo.Reservations);
    }

    [Fact]
    public async Task CreationHandlerReturnsFull_OrCountsIncludingNewPendingBooking()
    {
        var repo = new FakeParkingReservationRepository();
        var users = new FakeUserAccountRepository();
        var vehicles = new FakeVehicleRepository();
        var user = new UserAccount("hash", "09123456789"); user.Verify(); users.Users.Add(user);
        var vehicle = new Vehicle(user.Id, "ABC123", "Toyota", "qr", VehicleType.Car, verificationStatus: CorVerificationStatus.Verified);
        vehicles.Vehicles.Add(vehicle);
        var capacity = ReservationCapacityPolicy.GetCapacity(SystemSettingsStore.Current);
        repo.Reservations.AddRange(Enumerable.Range(0, capacity).Select(_ => Booking()));
        var handler = new CreateReservationHandler(repo, users, vehicles, new CreateReservationValidator(), new FakeNotificationSender(), new FakeEmailService());
        var command = new CreateReservationCommand(user.Id, Day, TimeSpan.FromHours(10), TimeSpan.FromHours(15), "Visit", VehicleId: vehicle.Id);
        var denied = await handler.Handle(command, default);
        Assert.False(denied.IsSuccess); Assert.Equal(ErrorCode.Conflict, denied.ErrorCode);
        Assert.Contains("No reservation spaces", denied.Message);
        repo.Reservations[0].Cancel();
        var created = await handler.Handle(command, default);
        Assert.True(created.IsSuccess, created.Message);
        Assert.Equal(capacity, created.Data!.Availability!.BookedSlots);
        Assert.Equal(0, created.Data.Availability.AvailableSlots);
    }

    [Fact]
    public async Task ApprovalDoesNotIgnoreRevisedCapacity()
    {
        var repo = new FakeParkingReservationRepository();
        var capacity = ReservationCapacityPolicy.GetCapacity(SystemSettingsStore.Current);
        repo.Reservations.AddRange(Enumerable.Range(0, capacity + 1).Select(_ => Booking()));
        var handler = new ApproveReservationHandler(repo, new FakeNotificationSender(), new FakeEmailService());
        var result = await handler.Handle(new(repo.Reservations[0].Id, Guid.NewGuid(), null), default);
        Assert.False(result.IsSuccess); Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal(ReservationStatus.Pending, repo.Reservations[0].Status);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(12, 10)]
    [InlineData(10, 25)]
    public async Task AvailabilityRejectsInvalidTimeRanges(int start, int end)
    {
        var result = await new GetReservationAvailabilityHandler(new FakeParkingReservationRepository())
            .Handle(new(Day, TimeSpan.FromHours(start), TimeSpan.FromHours(end)), default);
        Assert.False(result.IsSuccess); Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
    }
}
