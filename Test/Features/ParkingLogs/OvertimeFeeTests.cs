using System;
using Xunit;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace Test.Features.ParkingLogs;

// Regression: manual and reservation parking used to charge one flat amount however long the overstay was.
public class OvertimeFeeTests
{
    private static decimal Fee(EntryMethod method, double hours, bool reservation = false)
    {
        var booking = reservation
            ? new ParkingReservation(Guid.NewGuid(), "REF-1", DateTime.UtcNow, TimeSpan.FromHours(8), TimeSpan.FromHours(10), "test", ReservationType.Normal)
            : null;
        return ParkingCharges.Amount(VehicleType.Car, method, null, false, booking, hours);
    }

    [Fact]
    public void Overstay_GrowsWithHoursForEveryEntryType_AndFollowsTheMode()
    {
        var before = SystemSettingsStore.Current;
        try
        {
            var s = new SystemSettingsDto { FeeCalculationMode = "per_hour" };
            s.CarParking.ViolationRatePerHour = 15m;
            s.CarParking.EntryFee = 0m; s.CarParking.BaseFee = 50m; s.CarParking.DailyOverstayFee = 200m;
            SystemSettingsStore.Replace(s);

            foreach (var method in new[] { EntryMethod.Manual, EntryMethod.QrCode })
            {
                Assert.Equal(15m, Fee(method, 0.5));   // any started hour counts
                Assert.Equal(75m, Fee(method, 5));      // 5 hours
                Assert.Equal(150m, Fee(method, 9.2));   // 10 started hours
            }
            Assert.Equal(75m, Fee(EntryMethod.QrCode, 5, reservation: true));
            Assert.Equal(0m, Fee(EntryMethod.Manual, 0));

            s.FeeCalculationMode = "one_time"; SystemSettingsStore.Replace(s);
            Assert.Equal(50m, Fee(EntryMethod.Manual, 9));
            s.FeeCalculationMode = "one_time_hourly"; SystemSettingsStore.Replace(s);
            Assert.Equal(50m + 5 * 15m, Fee(EntryMethod.Manual, 5));
            s.FeeCalculationMode = "per_day"; SystemSettingsStore.Replace(s);
            Assert.Equal(200m, Fee(EntryMethod.Manual, 5));
            Assert.Equal(400m, Fee(EntryMethod.Manual, 30));
            s.FeeCalculationMode = "no_fee"; SystemSettingsStore.Replace(s);
            Assert.Equal(0m, Fee(EntryMethod.Manual, 9));
        }
        finally { SystemSettingsStore.Replace(before); }
    }
}
