using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Xunit;

namespace Test.Features.ParkingLogs;

public class NoParkingFeesTests
{


    [Theory]
    [InlineData(EntryMethod.QrCode)]
    [InlineData(EntryMethod.ManualScheduled)]
    public void ReservationCheckoutKeepsManilaExitGraceAndOverstayTracking(EntryMethod method)
    {
        var day = new DateTime(2026, 10, 28);
        var entry = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(day, TimeSpan.FromHours(8));
        var booking = new ParkingReservation(Guid.NewGuid(), "RES-FREE", day, TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Campus visit");
        var settings = new SystemSettingsDto { IsGracePeriodEnabled = true, GracePeriodMinutes = 15 };
        var deadline = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(day, new TimeSpan(9, 15, 0));
        var atDeadline = ActiveSessionTiming.ResolveCheckout(entry, method, null, booking, null, settings, deadline);
        var afterDeadline = ActiveSessionTiming.ResolveCheckout(entry, method, null, booking, null, settings, deadline.AddMinutes(1));
        Assert.Equal(deadline, atDeadline.MaximumExitTime); Assert.Equal(0, atDeadline.OverstayHours);
        Assert.Equal(1d / 60, afterDeadline.OverstayHours, 8);
    }

    [Fact]
    public void ManualOvernightCheckoutTracksElapsedOverstayWithoutACharge()
    {
        var day = new DateTime(2026, 10, 28);
        var entry = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(day, TimeSpan.FromHours(20));
        var exit = entry.AddHours(12);
        var timing = ActiveSessionTiming.ResolveCheckout(entry, EntryMethod.Manual, null, null, null, new(), exit);
        Assert.Equal(ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(day, new(23, 59, 59)), timing.MaximumExitTime);
        Assert.True(timing.OverstayHours > 8);
    }
}
