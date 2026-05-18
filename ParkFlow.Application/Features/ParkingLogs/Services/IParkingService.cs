using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

public interface IParkingService
{
    ParkingLog CreateEntry(Guid vehicleId, Guid guardId);
    void MarkExit(ParkingLog parkingLog);
    DateTime CalculateEntryGracePeriod(DateTime entryTime, TimeSpan startTime, int graceMinutes = 30);
    DateTime CalculateEstimatedExitTime(DateTime entryTime, TimeSpan endTime, int graceMinutes = 30);
    DateTime CalculateMaximumExitTime(DateTime entryTime, TimeSpan endTime, int graceMinutes = 30);
    double CalculateTotalParkingHours(DateTime entryTime, DateTime exitTime);
}
