using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

// A reservation only governs sessions that began in its entry window, not every later visit that day.
public static class ReservationSessionWindow
{
    /// <summary>
    /// A timed reservation stays usable after an early exit, so the holder can come back until it ends.
    /// </summary>
    public static bool StillOpenAfterExit(ParkingReservation reservation, DateTime exitUtc)
    {
        if (reservation.Type == ReservationType.Special) return false;
        var exit = ParkingTimeHelper.ConvertUtcToPhilippinesTime(exitUtc);
        var date = reservation.ReservationDate.Date;
        var localDate = ParkingTimeHelper.ConvertUtcToPhilippinesTime(reservation.ReservationDate).Date;
        return (date == exit.Date || localDate == exit.Date) && exit.TimeOfDay < reservation.EndTime;
    }

    public static bool CoversEntry(ParkingReservation reservation, DateTime entryUtc)
    {
        var entry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(entryUtc);
        var date = reservation.ReservationDate.Date;
        var localDate = ParkingTimeHelper.ConvertUtcToPhilippinesTime(reservation.ReservationDate).Date;
        if (date != entry.Date && localDate != entry.Date) return false;
        if (reservation.Type == ReservationType.Special) return true;
        return entry.TimeOfDay >= reservation.StartTime &&
            entry.TimeOfDay <= reservation.EndTime;
    }
}
