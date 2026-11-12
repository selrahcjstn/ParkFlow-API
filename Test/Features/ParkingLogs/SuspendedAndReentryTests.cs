using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Xunit;

namespace Test.Features.ParkingLogs;

public class SuspendedAndReentryTests
{
    [Fact]
    public void SuspendedAccountIsBlockedBeforeAnyOtherRequirement()
    {
        var account = (UserAccount)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UserAccount));
        typeof(UserAccount).GetProperty("Status")!.SetValue(account, AccountStatus.Suspended);
        var profile = (UserProfile)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UserProfile));
        typeof(UserProfile).GetProperty("UserAccount")!.SetValue(profile, account);
        Assert.Equal(DriverEntryPolicy.SuspendedMessage, DriverEntryPolicy.MissingRequirement(profile));
    }

    [Theory]
    [InlineData(16, true)]   // 4 PM, reservation until 8 PM: still open
    [InlineData(20, false)]  // at the end time: finished
    [InlineData(21, false)]
    public void EarlyExitKeepsATimedReservationOpen(int phHour, bool stillOpen)
    {
        var day = new DateTime(2026, 11, 12);
        var reservation = new ParkingReservation(Guid.NewGuid(), "RES-1", day, TimeSpan.FromHours(7), TimeSpan.FromHours(20), "Visit");
        var exitUtc = day.AddHours(phHour - 8);
        Assert.Equal(stillOpen, ReservationSessionWindow.StillOpenAfterExit(reservation, exitUtc));
    }
}
