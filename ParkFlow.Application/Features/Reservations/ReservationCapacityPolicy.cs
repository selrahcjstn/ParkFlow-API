using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Reservations;

public record ReservationTimeWindow(TimeSpan StartTime, TimeSpan EndTime);
public record ReservationAvailability(int TotalCapacity,
    int ReservationCapacity, int BookedSlots, int AvailableSlots, VehicleType? VehicleType = null);
public enum ReservationBookingResult { Created, CapacityFull, DuplicateUser, DuplicateVehicle }

public static class ReservationCapacityPolicy
{
    public static int GetCapacity(SystemSettingsDto settings, VehicleType? type = null) => type.HasValue
        ? Math.Clamp(settings.ForVehicle(type.Value).ReservationAllocationSlots, 0, Math.Max(0, settings.ForVehicle(type.Value).TotalCapacity))
        : Math.Clamp(settings.ReservationAllocationSlots, 0, Math.Max(0, settings.TotalCapacity));

    // Half-open intervals: an ending booking releases its space before a new one starts.
    public static int GetPeak(IEnumerable<ReservationTimeWindow> windows, TimeSpan start, TimeSpan end)
    {
        var events = windows.Where(w => w.StartTime < end && w.EndTime > start)
            .SelectMany(w => new[] {
                (Time: w.StartTime < start ? start : w.StartTime, Delta: 1),
                (Time: w.EndTime > end ? end : w.EndTime, Delta: -1)
            }).OrderBy(e => e.Time).ThenBy(e => e.Delta);
        var active = 0;
        var peak = 0;
        foreach (var e in events) { active += e.Delta; peak = Math.Max(peak, active); }
        return peak;
    }

    public static ReservationAvailability Describe(SystemSettingsDto settings, int booked, VehicleType? type = null)
    {
        var capacity = GetCapacity(settings, type);
        return new(type.HasValue ? settings.ForVehicle(type.Value).TotalCapacity : settings.TotalCapacity, capacity, booked, Math.Max(0, capacity - booked), type);
    }
}
