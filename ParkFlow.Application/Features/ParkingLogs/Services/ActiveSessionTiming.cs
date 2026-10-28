using ParkFlow.Application.Common;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

// The dashboard count and session list must use the same deadline selection.
// Inputs are already batch-loaded by their handlers; no per-session queries.
public static class ActiveSessionTiming
{
    public static (DateTime? MaximumExitTime, double OverstayHours) ResolveActive(
        ParkingLog log, DateTime nowUtc, SystemSettingsDto settings,
        IEnumerable<CorSubmission> submissions, ILookup<Guid, ParkingSchedule> schedules,
        IEnumerable<ParkingReservation> reservations, Personnel? personnel = null, Vehicle? vehicle = null)
    {
        vehicle ??= log.Vehicle;
        personnel ??= vehicle.Owner?.UserProfile?.Personnel;
        var timing = Resolve(log, nowUtc, settings, submissions, schedules, reservations, personnel, vehicle);
        return (PersonnelParkingPolicy.IsFaculty(personnel) ? null : timing.DeadlineUtc,
            timing.IsSpecialReservation ? 0 : GetOverstayHours(log, nowUtc, settings, timing.DeadlineUtc, personnel));
    }

    public static (DateTime? MaximumExitTime, double OverstayHours) ResolveCheckout(
        DateTime entryUtc, EntryMethod method, Personnel? personnel, ParkingReservation? reservation,
        ParkingSchedule? schedule, SystemSettingsDto settings, DateTime exitUtc)
    {
        if (PersonnelParkingPolicy.IsFaculty(personnel)) return (null, 0);
        var entry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(entryUtc);
        if (PersonnelParkingPolicy.AppliesTo(personnel))
            return (PersonnelParkingPolicy.GetDeadlineUtc(entryUtc, settings),
                PersonnelParkingPolicy.GetChargeDuration(entryUtc, exitUtc, settings).TotalHours);
        DateTime deadline;
        var grace = settings.IsGracePeriodEnabled ? settings.GracePeriodMinutes : 0;
        if (method == EntryMethod.Manual || reservation?.Type == ReservationType.Special)
            deadline = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entry, new TimeSpan(23, 59, 59));
        else if (reservation != null)
            deadline = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entry, reservation.EndTime).AddMinutes(grace);
        else if (schedule != null)
            deadline = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entry, schedule.EndTime).AddMinutes(grace);
        else
        {
            deadline = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entry, TimeSpan.FromHours(22));
            if (deadline <= entryUtc) deadline = entryUtc.AddHours(4);
        }
        return (deadline, reservation?.Type == ReservationType.Special ? 0 : Math.Max(0, (exitUtc - deadline).TotalHours));
    }

    public static (DateTime DeadlineUtc, bool HasReservation, bool IsSpecialReservation) Resolve(
        ParkingLog log, DateTime nowUtc, SystemSettingsDto settings,
        IEnumerable<CorSubmission> submissions,
        ILookup<Guid, ParkingSchedule> schedulesBySubmission,
        IEnumerable<ParkingReservation> reservations, Personnel? personnel = null, Vehicle? vehicle = null)
    {
        var entry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);
        vehicle ??= log.Vehicle;
        personnel ??= vehicle.Owner?.UserProfile?.Personnel;
        if (PersonnelParkingPolicy.AppliesTo(personnel))
            return (PersonnelParkingPolicy.GetDeadlineUtc(log.EntryTime, settings), false, false);

        if (log.EntryMethod == EntryMethod.Manual)
            return (ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entry, new TimeSpan(23, 59, 59)), false, false);

        bool DateMatches(ParkingReservation reservation)
        {
            return ReservationSessionWindow.CoversEntry(reservation, log.EntryTime);
        }
        bool VehicleMatches(ParkingReservation reservation) =>
            reservation.VehicleId == log.VehicleId || reservation.VehicleId == null || reservation.VehicleId == Guid.Empty;
        bool IsReviewed(ParkingReservation reservation) =>
            reservation.Status is ReservationStatus.Approved or ReservationStatus.Completed;
        bool IsEligible(ParkingReservation reservation) =>
            reservation.Status is not ReservationStatus.Cancelled and not ReservationStatus.Rejected;

        var userReservations = reservations.ToList();
        var reservation = userReservations.FirstOrDefault(r => VehicleMatches(r) && DateMatches(r) && IsReviewed(r))
            ?? userReservations.FirstOrDefault(r => DateMatches(r) && IsReviewed(r))
            ?? userReservations.FirstOrDefault(r => VehicleMatches(r) && DateMatches(r) && IsEligible(r))
            ?? userReservations.FirstOrDefault(r => DateMatches(r) && IsEligible(r));
        var grace = settings.IsGracePeriodEnabled ? settings.GracePeriodMinutes : 0;
        if (reservation != null)
        {
            var end = reservation.Type == ReservationType.Special ? new TimeSpan(23, 59, 59) : reservation.EndTime;
            var deadline = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entry, end);
            return (reservation.Type == ReservationType.Special ? deadline : deadline.AddMinutes(grace), true, reservation.Type == ReservationType.Special);
        }

        // Match checkout's authoritative verified-COR selection, including legacy
        // split/adjacent class intervals. Unverified documents cannot set deadlines.
        var verified = submissions.FirstOrDefault(c => c.UserAccountId == vehicle.OwnerId &&
            c.VerificationStatus == CorVerificationStatus.Verified);
        var schedule = verified == null ? null : ScheduleService.FindEntrySchedule(schedulesBySubmission[verified.Id], entry);
        if (schedule != null)
            return (ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entry, schedule.EndTime).AddMinutes(grace), false, false);

        var closing = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entry, TimeSpan.FromHours(22));
        return (closing > log.EntryTime ? closing : log.EntryTime.AddHours(4), false, false);
    }

    public static double GetOverstayHours(ParkingLog log, DateTime nowUtc, SystemSettingsDto settings, DateTime deadlineUtc, Personnel? personnel = null)
    {
        personnel ??= log.Vehicle?.Owner?.UserProfile?.Personnel;
        if (PersonnelParkingPolicy.IsFaculty(personnel)) return 0;
        if (PersonnelParkingPolicy.AppliesTo(personnel))
            return PersonnelParkingPolicy.GetChargeDuration(log.EntryTime, nowUtc, settings).TotalHours;
        if (log.EntryMethod == EntryMethod.Manual &&
            ParkingTimeHelper.ConvertUtcToPhilippinesTime(nowUtc).Date <=
            ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime).Date)
            return 0;
        return Math.Max(0, (nowUtc - deadlineUtc).TotalHours);
    }
}
