using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;

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

    // Compatibility methods retained until legacy violation services are removed in Phase 3.
    public decimal CalculatePenalty(TimeSpan overstayDuration, decimal? customHourlyRate = null) => 0m;
    public decimal CalculatePenaltyForVehicle(TimeSpan duration, VehicleType vehicleType) => 0m;
}
