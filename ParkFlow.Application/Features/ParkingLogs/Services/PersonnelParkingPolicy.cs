using System.Globalization;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Services;

// One rule shared by entry, live session displays and checkout. A free window
// belongs to the entry date: an unclosed session never gets another free day.
public static class PersonnelParkingPolicy
{
    public static bool AppliesTo(Personnel? personnel) => personnel?.Role is
        Roles.UniversityStaff or Roles.NonAcademicPersonnel;

    public static bool TryGetHours(SystemSettingsDto settings, out TimeSpan start, out TimeSpan end)
    {
        string[] formats = [@"hh\:mm", @"hh\:mm\:ss"];
        var validStart = TimeSpan.TryParseExact(settings.PersonnelFreeParkingStart, formats,
            CultureInfo.InvariantCulture, out start);
        var validEnd = TimeSpan.TryParseExact(settings.PersonnelFreeParkingEnd, formats,
            CultureInfo.InvariantCulture, out end);
        return validStart && validEnd && start >= TimeSpan.Zero && end < TimeSpan.FromDays(1) && start < end;
    }

    public static DateTime GetDeadlineUtc(DateTime entryUtc, SystemSettingsDto settings)
    {
        if (!TryGetHours(settings, out _, out var end))
            throw new ArgumentException("Faculty/staff parking hours must be a valid same-day window.");
        return ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(
            ParkingTimeHelper.ConvertUtcToPhilippinesTime(entryUtc), end);
    }

    public static TimeSpan GetChargeDuration(DateTime entryUtc, DateTime nowUtc, SystemSettingsDto settings)
    {
        if (nowUtc <= entryUtc) return TimeSpan.Zero;
        if (!TryGetHours(settings, out var start, out _))
            throw new ArgumentException("Faculty/staff parking hours must be a valid same-day window.");
        var entryLocal = ParkingTimeHelper.ConvertUtcToPhilippinesTime(entryUtc);
        var freeStartUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entryLocal, start);
        var freeEndUtc = GetDeadlineUtc(entryUtc, settings);
        var earlyEnd = nowUtc < freeStartUtc ? nowUtc : freeStartUtc;
        var early = earlyEnd > entryUtc ? earlyEnd - entryUtc : TimeSpan.Zero;
        var lateStart = entryUtc > freeEndUtc ? entryUtc : freeEndUtc;
        var late = nowUtc > lateStart ? nowUtc - lateStart : TimeSpan.Zero;
        return early + late;
    }

    public static decimal CalculateCharge(DateTime entryUtc, DateTime nowUtc, SystemSettingsDto settings)
    {
        var duration = GetChargeDuration(entryUtc, nowUtc, settings);
        return duration <= TimeSpan.Zero ? 0m
            : (decimal)Math.Ceiling(duration.TotalHours) * Math.Max(0m, settings.ViolationRatePerHour);
    }
}
