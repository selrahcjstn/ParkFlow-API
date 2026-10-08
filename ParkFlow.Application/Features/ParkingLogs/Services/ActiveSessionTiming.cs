using ParkFlow.Application.Common;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

// The dashboard count and session list must use the same deadline selection.
// Inputs are already batch-loaded by their handlers; no per-session queries.
public static class ActiveSessionTiming
{
    public static (DateTime DeadlineUtc, bool HasReservation) Resolve(
        ParkingLog log, DateTime nowUtc, SystemSettingsDto settings,
        IEnumerable<CorSubmission> submissions,
        ILookup<Guid, ParkingSchedule> schedulesBySubmission,
        IEnumerable<ParkingReservation> reservations)
    {
        var entry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);
        if (PersonnelParkingPolicy.AppliesTo(log.Vehicle.Owner?.UserProfile?.Personnel))
            return (PersonnelParkingPolicy.GetDeadlineUtc(log.EntryTime, settings), false);

        if (log.EntryMethod == EntryMethod.Manual)
            return (ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entry, new TimeSpan(23, 59, 59)), false);

        var nowDate = ParkingTimeHelper.ConvertUtcToPhilippinesTime(nowUtc).Date;
        bool DateMatches(ParkingReservation reservation)
        {
            var date = reservation.ReservationDate.Date;
            var localDate = ParkingTimeHelper.ConvertUtcToPhilippinesTime(reservation.ReservationDate).Date;
            return date == entry.Date || localDate == entry.Date || date == nowDate || localDate == nowDate;
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
            return (reservation.Type == ReservationType.Special ? deadline : deadline.AddMinutes(grace), true);
        }

        var userSubmissions = submissions.Where(c => c.UserAccountId == log.Vehicle.OwnerId).ToList();
        var verified = userSubmissions.FirstOrDefault(c => c.VerificationStatus == CorVerificationStatus.Verified);
        var schedules = verified == null ? new List<ParkingSchedule>() : schedulesBySubmission[verified.Id].ToList();
        if (schedules.Count == 0)
        {
            var latest = userSubmissions.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
            schedules = latest == null ? [] : schedulesBySubmission[latest.Id].ToList();
            if (schedules.Count == 0)
                schedules = userSubmissions.SelectMany(c => schedulesBySubmission[c.Id])
                    .OrderByDescending(s => s.CreatedAt).GroupBy(s => s.DayOfWeek).Select(g => g.First()).ToList();
        }
        var daySchedules = schedules.Where(s => s.DayOfWeek == entry.DayOfWeek).OrderBy(s => s.StartTime).ToList();
        var schedule = daySchedules.FirstOrDefault(s => entry.TimeOfDay >= s.StartTime && entry.TimeOfDay <= s.EndTime)
            ?? daySchedules.LastOrDefault();
        if (schedule != null)
            return (ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entry, schedule.EndTime).AddMinutes(grace), false);

        var closing = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entry, TimeSpan.FromHours(22));
        return (closing > log.EntryTime ? closing : log.EntryTime.AddHours(4), false);
    }

    public static double GetOverstayHours(ParkingLog log, DateTime nowUtc, SystemSettingsDto settings, DateTime deadlineUtc)
    {
        if (PersonnelParkingPolicy.AppliesTo(log.Vehicle.Owner?.UserProfile?.Personnel))
            return PersonnelParkingPolicy.GetChargeDuration(log.EntryTime, nowUtc, settings).TotalHours;
        if (log.EntryMethod == EntryMethod.Manual &&
            ParkingTimeHelper.ConvertUtcToPhilippinesTime(nowUtc).Date <=
            ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime).Date)
            return 0;
        return Math.Max(0, (nowUtc - deadlineUtc).TotalHours);
    }
}
