using ParkFlow.Application.Common;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

// A reservation only governs sessions that began in its entry window, not every later visit that day.
public static class ReservationSessionWindow
{
    public static bool CoversEntry(ParkingReservation reservation, DateTime entryUtc, SystemSettingsDto settings)
    {
        var entry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(entryUtc);
        var date = reservation.ReservationDate.Date;
        var localDate = ParkingTimeHelper.ConvertUtcToPhilippinesTime(reservation.ReservationDate).Date;
        if (date != entry.Date && localDate != entry.Date) return false;
        if (reservation.Type == ReservationType.Special) return true;
        var earlyMinutes = settings.IsEarlyParkingAllowed ? settings.EarlyParkingMinutes : 0;
        return entry.TimeOfDay >= reservation.StartTime.Subtract(TimeSpan.FromMinutes(earlyMinutes)) &&
            entry.TimeOfDay <= reservation.EndTime;
    }
}
