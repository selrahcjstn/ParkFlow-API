namespace ParkFlow.Application.Features.ParkingLogs.Services;

public interface IViolationService
{
    bool IsOverstay(DateTime exitTime, TimeSpan scheduleEndTime, int graceMinutes = 30);
    TimeSpan GetOverstayDuration(DateTime exitTime, TimeSpan scheduleEndTime, int graceMinutes = 30);
    bool IsOverstay(DateTime exitTime, DateTime maximumExitTime);
    TimeSpan GetOverstayDuration(DateTime exitTime, DateTime maximumExitTime);
    decimal CalculatePenalty(TimeSpan overstayDuration, decimal hourlyRate = 5m);
}
