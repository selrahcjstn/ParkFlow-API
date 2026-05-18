using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

public class ScheduleService : IScheduleService
{
    public bool CanEnter(DateTime currentTime, ParkingSchedule schedule, int graceMinutes = 30)
    {
        var entryTimeOfDay = currentTime.TimeOfDay;
        var earliestAllowedEntry = GetEarliestAllowedEntryTime(schedule, graceMinutes);

        return entryTimeOfDay >= earliestAllowedEntry && entryTimeOfDay <= schedule.EndTime;
    }

    public TimeSpan GetEarliestAllowedEntryTime(ParkingSchedule schedule, int graceMinutes = 30)
    {
        return schedule.StartTime.Add(TimeSpan.FromMinutes(-graceMinutes));
    }
}
