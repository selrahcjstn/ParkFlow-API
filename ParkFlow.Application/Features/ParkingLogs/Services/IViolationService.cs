using ParkFlow.Domain.Enums;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

public interface IViolationService
{
    bool IsOverstay(DateTime exitTime, TimeSpan scheduleEndTime, int? graceMinutes = null);
    TimeSpan GetOverstayDuration(DateTime exitTime, TimeSpan scheduleEndTime, int? graceMinutes = null);
    bool IsOverstay(DateTime exitTime, DateTime maximumExitTime);
    TimeSpan GetOverstayDuration(DateTime exitTime, DateTime maximumExitTime);
    decimal CalculatePenalty(TimeSpan overstayDuration, decimal? customHourlyRate = null);
    decimal CalculatePenaltyForVehicle(TimeSpan duration, VehicleType vehicleType) =>
        CalculatePenalty(duration, SystemSettingsStore.Current.ForVehicle(vehicleType).ViolationRatePerHour);
}
