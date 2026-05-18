namespace ParkFlow.Application.Features.ParkingLogs.Services;

public class ViolationService : IViolationService
{
    public bool IsOverstay(DateTime exitTime, TimeSpan scheduleEndTime, int graceMinutes = 30)
    {
        var allowedExitTime = scheduleEndTime.Add(TimeSpan.FromMinutes(graceMinutes));
        return exitTime.TimeOfDay > allowedExitTime;
    }

    public TimeSpan GetOverstayDuration(DateTime exitTime, TimeSpan scheduleEndTime, int graceMinutes = 30)
    {
        var allowedExitTime = scheduleEndTime.Add(TimeSpan.FromMinutes(graceMinutes));
        var duration = exitTime.TimeOfDay - allowedExitTime;
        return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
    }

    public decimal CalculatePenalty(TimeSpan overstayDuration, decimal hourlyRate = 5m)
    {
        var hours = Math.Max(0, Math.Ceiling(overstayDuration.TotalHours));
        return (decimal)hours * hourlyRate;
    }
}
