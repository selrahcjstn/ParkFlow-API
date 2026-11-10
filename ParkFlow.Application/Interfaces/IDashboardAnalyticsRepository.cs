using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ParkFlow.Application.Features.Dashboard.Queries.GetDashboardAnalytics;

namespace ParkFlow.Application.Interfaces;

/// <summary>
/// Read-only, database-side aggregations for the admin dashboard.
/// All day/hour bucketing is done in Philippine local time.
/// </summary>
public interface IDashboardAnalyticsRepository
{
    Task<AccountBreakdownDto> GetAccountBreakdownAsync(CancellationToken ct);
    Task<VehicleBreakdownDto> GetVehicleBreakdownAsync(CancellationToken ct);
    Task<PendingWorkDto> GetPendingWorkAsync(CancellationToken ct);

    /// <summary>Entries and exits (registered + visitor) inside [startUtc, endUtc).</summary>
    Task<(int Entries, int Exits)> GetEntryExitTotalsAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct);
    Task<int> GetCurrentlyInsideAsync(CancellationToken ct);

    /// <summary>Only buckets that have data are returned; the caller zero-fills.</summary>
    Task<List<ActivityCountRow>> GetActivityBucketsAsync(DateTime startUtc, DateTime endUtc, bool hourly, CancellationToken ct);
    Task<List<HourCountRow>> GetHourOfDayAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct);

    Task<List<RecentParkingActivityDto>> GetRecentActivityAsync(int take, CancellationToken ct);
}
