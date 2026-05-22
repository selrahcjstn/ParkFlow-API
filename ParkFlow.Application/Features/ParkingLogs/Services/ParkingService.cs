using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

public class ParkingService : IParkingService
{
    public ParkingLog CreateEntry(Guid vehicleId, Guid guardId)
    {
        return new ParkingLog(vehicleId, guardId, ParkingStatus.Parked);
    }

    public void MarkExit(ParkingLog parkingLog)
    {
        parkingLog.Exit();
    }

    public DateTime CalculateEntryGracePeriod(DateTime entryTime, TimeSpan startTime, int graceMinutes = 30)
    {
        return entryTime.Date.Add(startTime).AddMinutes(-graceMinutes);
    }

    public DateTime CalculateEstimatedExitTime(DateTime entryTime, TimeSpan endTime, int graceMinutes = 30)
    {
        return entryTime.Date.Add(endTime).AddMinutes(graceMinutes);
    }

    public DateTime CalculateMaximumExitTime(DateTime entryTime, TimeSpan endTime, int graceMinutes = 30)
    {
        return CalculateEstimatedExitTime(entryTime, endTime, graceMinutes);
    }

    public double CalculateTotalParkingHours(DateTime entryTime, DateTime exitTime)
    {
        var duration = exitTime - entryTime;
        return Math.Max(0, duration.TotalHours);
    }
}
