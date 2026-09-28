using ParkFlow.Application.Common;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

public class ScheduleService : IScheduleService
{
    public bool CanEnter(DateTime currentTime, ParkingSchedule schedule, int? earlyBufferMinutes = null)
    {
        var entryTimeOfDay = currentTime.TimeOfDay;
        var earliestAllowedEntry = GetEarliestAllowedEntryTime(schedule, earlyBufferMinutes);

        return entryTimeOfDay >= earliestAllowedEntry && entryTimeOfDay <= schedule.EndTime;
    }

    public TimeSpan GetEarliestAllowedEntryTime(ParkingSchedule schedule, int? earlyBufferMinutes = null)
    {
        var settings = SystemSettingsStore.Current;
        var buffer = earlyBufferMinutes ?? (settings.IsEarlyParkingAllowed ? settings.EarlyParkingMinutes : 0);
        return schedule.StartTime.Subtract(TimeSpan.FromMinutes(buffer));
    }
}
