namespace ParkFlow.Application.Features.ParkingLogs.Services;

public interface IViolationService
{
    bool IsOverstay(DateTime exitTime, TimeSpan scheduleEndTime, int graceMinutes = 30);
    TimeSpan GetOverstayDuration(DateTime exitTime, TimeSpan scheduleEndTime, int graceMinutes = 30);
    decimal CalculatePenalty(TimeSpan overstayDuration, decimal hourlyRate = 5m);
}
