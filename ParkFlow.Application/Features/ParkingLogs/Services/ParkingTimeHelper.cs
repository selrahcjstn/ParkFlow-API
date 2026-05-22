namespace ParkFlow.Application.Features.ParkingLogs.Services;

public static class ParkingTimeHelper
{
    private const string PhilippinesTimeZoneId = "Singapore Standard Time";

    public static DateTime ConvertUtcToPhilippinesTime(DateTime utcDateTime)
    {
        var philippinesTimeZone = TimeZoneInfo.FindSystemTimeZoneById(PhilippinesTimeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, philippinesTimeZone);
    }

    public static DateTime BuildPhilippinesScheduleUtcDateTime(DateTime philippinesDate, TimeSpan scheduleTime)
    {
        var philippinesTimeZone = TimeZoneInfo.FindSystemTimeZoneById(PhilippinesTimeZoneId);

        var localScheduleDateTime = new DateTime(
            philippinesDate.Year,
            philippinesDate.Month,
            philippinesDate.Day,
            scheduleTime.Hours,
            scheduleTime.Minutes,
            scheduleTime.Seconds,
            DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(localScheduleDateTime, philippinesTimeZone);
    }
}
