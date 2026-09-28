using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

public interface IScheduleService
{
    bool CanEnter(DateTime currentTime, ParkingSchedule schedule, int? earlyBufferMinutes = null);
    TimeSpan GetEarliestAllowedEntryTime(ParkingSchedule schedule, int? earlyBufferMinutes = null);
}
