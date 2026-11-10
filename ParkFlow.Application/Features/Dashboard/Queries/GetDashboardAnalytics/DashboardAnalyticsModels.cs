using System;
using System.Collections.Generic;

namespace ParkFlow.Application.Features.Dashboard.Queries.GetDashboardAnalytics;

public enum ActivityRange { Today, Last7Days, Last30Days }

/// <summary>One time bucket (an hour for "today", a Philippine calendar day otherwise).</summary>
public record ActivityBucketDto(string Key, string Label, int Entries, int Exits);

/// <summary>Hour of the Philippine day (0-23) aggregated over a window.</summary>
public record HourOfDayDto(int Hour, string Label, int Entries, int Exits);

public record DashboardActivityResponse(
    string Range,
    string Granularity,
    string Timezone,
    DateTime StartUtc,
    DateTime EndUtc,
    int PeakWindowDays,
    int TotalEntries,
    int TotalExits,
    List<ActivityBucketDto> Series,
    List<HourOfDayDto> PeakHours);

public record AccountBreakdownDto(
    int Total, int Students, int Personnel, int Guards, int Admins,
    int Active, int PendingVerification, int Suspended);

public record VehicleBreakdownDto(
    int Total, int Motorcycles, int Cars, int ElectricBikes,
    int Pending, int Approved, int Rejected);

public record PendingWorkDto(int Accounts, int Vehicles, int CorSchedules);

public record TodayOperationsDto(int Entries, int Exits, int YesterdayEntries, int YesterdayExits, int CurrentlyInside);

public record RecentParkingActivityDto(
    string Source,
    string Identifier,
    string Owner,
    string VehicleType,
    DateTime EntryTime,
    DateTime? ExitTime,
    string Status);

public record DashboardOverviewResponse(
    AccountBreakdownDto Accounts,
    VehicleBreakdownDto Vehicles,
    PendingWorkDto Pending,
    TodayOperationsDto Today,
    List<RecentParkingActivityDto> Recent,
    DateTime GeneratedAtUtc);

/// <summary>Raw rows returned by the repository; zero-filling happens in the handler.</summary>
public record ActivityCountRow(DateTime LocalBucket, int Entries, int Exits);
public record HourCountRow(int Hour, int Entries, int Exits);
