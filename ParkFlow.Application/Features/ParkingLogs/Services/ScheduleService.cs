using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

public class ScheduleService : IScheduleService
{
    public static ParkingSchedule? FindEntrySchedule(IEnumerable<ParkingSchedule> schedules, DateTime entry)
    {
        var day = schedules.Where(s => s.DayOfWeek == entry.DayOfWeek).OrderBy(s => s.StartTime).ToList();
        var selected = day.LastOrDefault(s => entry.TimeOfDay >= s.StartTime && entry.TimeOfDay <= s.EndTime);
        if (selected == null) return day.FirstOrDefault(); // Retain the legacy override behavior outside submitted hours.
        var start = selected.StartTime; var end = selected.EndTime;
        // Consecutive classes have no break; keep the continuous permitted interval.
        foreach (var next in day.Where(s => s.StartTime >= start))
        {
            if (next.StartTime > end) break;
            if (next.EndTime > end) end = next.EndTime;
        }
        return new ParkingSchedule(selected.SubmissionId, selected.DayOfWeek, start, end);
    }

    public bool CanEnter(DateTime currentTime, ParkingSchedule schedule)
    {
        var entryTimeOfDay = currentTime.TimeOfDay;
        var earliestAllowedEntry = GetEarliestAllowedEntryTime(schedule);

        return entryTimeOfDay >= earliestAllowedEntry && entryTimeOfDay <= schedule.EndTime;
    }

    public TimeSpan GetEarliestAllowedEntryTime(ParkingSchedule schedule)
    {
        return schedule.StartTime;
    }
}
