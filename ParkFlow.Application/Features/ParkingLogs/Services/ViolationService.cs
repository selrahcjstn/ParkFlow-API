using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

public class ViolationService : IViolationService
{
    public bool IsOverstay(DateTime exitTime, TimeSpan scheduleEndTime, int? graceMinutes = null)
    {
        var settings = SystemSettingsStore.Current;
        var effectiveGrace = graceMinutes ?? (settings.IsGracePeriodEnabled ? settings.GracePeriodMinutes : 0);
        var allowedExitTime = scheduleEndTime.Add(TimeSpan.FromMinutes(effectiveGrace));
        return exitTime.TimeOfDay > allowedExitTime;
    }

    public TimeSpan GetOverstayDuration(DateTime exitTime, TimeSpan scheduleEndTime, int? graceMinutes = null)
    {
        var settings = SystemSettingsStore.Current;
        var effectiveGrace = graceMinutes ?? (settings.IsGracePeriodEnabled ? settings.GracePeriodMinutes : 0);
        var allowedExitTime = scheduleEndTime.Add(TimeSpan.FromMinutes(effectiveGrace));
        var duration = exitTime.TimeOfDay - allowedExitTime;
        return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
    }

    public bool IsOverstay(DateTime exitTime, DateTime maximumExitTime)
    {
        return exitTime > maximumExitTime;
    }

    public TimeSpan GetOverstayDuration(DateTime exitTime, DateTime maximumExitTime)
    {
        var duration = exitTime - maximumExitTime;
        return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
    }

    public decimal CalculatePenalty(TimeSpan overstayDuration, decimal? customHourlyRate = null)
    {
        if (overstayDuration <= TimeSpan.Zero)
            return 0m;

        var settings = SystemSettingsStore.Current;
        var mode = (settings.FeeCalculationMode ?? "per_hour").ToLowerInvariant();

        if (mode == "no_fee")
            return 0m;

        var hours = Math.Max(1, (int)Math.Ceiling(overstayDuration.TotalHours));
        var hourlyRate = customHourlyRate ?? settings.ViolationRatePerHour;
        var baseFee = settings.BaseFee;

        return mode switch
        {
            "per_day" => hourlyRate,
            "one_time" => baseFee > 0m ? baseFee : hourlyRate,
            "one_time_hourly" => baseFee + (hours * hourlyRate),
            "per_hour" or _ => hours * hourlyRate,
        };
    }
}
