using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

public interface IParkingService
{
    ParkingLog CreateEntry(Guid vehicleId, Guid guardId, EntryMethod entryMethod = EntryMethod.QrCode);
    void MarkExit(ParkingLog parkingLog);
    DateTime CalculateEntryGracePeriod(DateTime entryTime, TimeSpan startTime, int? earlyBufferMinutes = null);
    DateTime CalculateEstimatedExitTime(DateTime entryTime, TimeSpan endTime, int? graceMinutes = null);
    DateTime CalculateMaximumExitTime(DateTime entryTime, TimeSpan endTime, int? graceMinutes = null);
    double CalculateTotalParkingHours(DateTime entryTime, DateTime exitTime);
}
