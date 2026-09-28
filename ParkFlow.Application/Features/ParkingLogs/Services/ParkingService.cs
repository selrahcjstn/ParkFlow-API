using ParkFlow.Application.Common;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

public class ParkingService : IParkingService
{
    public ParkingLog CreateEntry(Guid vehicleId, Guid? guardId, EntryMethod entryMethod = EntryMethod.QrCode)
    {
        return new ParkingLog(vehicleId, guardId, ParkingStatus.Parked, entryMethod);
    }

    public void MarkExit(ParkingLog parkingLog)
    {
        parkingLog.Exit();
    }

    public DateTime CalculateEntryGracePeriod(DateTime entryTime, TimeSpan startTime, int? earlyBufferMinutes = null)
    {
        var settings = SystemSettingsStore.Current;
        var buffer = earlyBufferMinutes ?? (settings.IsEarlyParkingAllowed ? settings.EarlyParkingMinutes : 0);
        return entryTime.Date.Add(startTime).AddMinutes(-buffer);
    }

    public DateTime CalculateEstimatedExitTime(DateTime entryTime, TimeSpan endTime, int? graceMinutes = null)
    {
        var settings = SystemSettingsStore.Current;
        var grace = graceMinutes ?? (settings.IsGracePeriodEnabled ? settings.GracePeriodMinutes : 0);
        return entryTime.Date.Add(endTime).AddMinutes(grace);
    }

    public DateTime CalculateMaximumExitTime(DateTime entryTime, TimeSpan endTime, int? graceMinutes = null)
    {
        return CalculateEstimatedExitTime(entryTime, endTime, graceMinutes);
    }

    public double CalculateTotalParkingHours(DateTime entryTime, DateTime exitTime)
    {
        var duration = exitTime - entryTime;
        return Math.Max(0, duration.TotalHours);
    }
}
