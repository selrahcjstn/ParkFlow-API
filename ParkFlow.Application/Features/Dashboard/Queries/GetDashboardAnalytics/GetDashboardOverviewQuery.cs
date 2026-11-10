using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Dashboard.Queries.GetDashboardAnalytics;

public record GetDashboardOverviewQuery : IRequest<Result<DashboardOverviewResponse>>;

public class GetDashboardOverviewHandler(IDashboardAnalyticsRepository repository, ICacheService? cache = null)
    : IRequestHandler<GetDashboardOverviewQuery, Result<DashboardOverviewResponse>>
{
    public async Task<Result<DashboardOverviewResponse>> Handle(GetDashboardOverviewQuery request, CancellationToken ct)
    {
        try
        {
            var response = cache == null
                ? await LoadAsync(ct)
                : await cache.GetOrCreateAsync(CacheKeys.DashboardOverview, () => LoadAsync(ct), TimeSpan.FromSeconds(30), ct);
            return Result<DashboardOverviewResponse>.Success(response!, "Dashboard overview retrieved.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<DashboardOverviewResponse>.Failure($"Could not load dashboard overview: {ex.Message}", ErrorCode.ServerError);
        }
    }

    private async Task<DashboardOverviewResponse> LoadAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var (todayStart, tomorrowStart) = DashboardTime.DayBoundsUtc(now, 0);
        var (yesterdayStart, _) = DashboardTime.DayBoundsUtc(now, -1);

        // The repository shares one scoped DbContext, so these run sequentially.
        var accounts = await repository.GetAccountBreakdownAsync(ct);
        var vehicles = await repository.GetVehicleBreakdownAsync(ct);
        var pending = await repository.GetPendingWorkAsync(ct);
        var today = await repository.GetEntryExitTotalsAsync(todayStart, tomorrowStart, ct);
        var yesterday = await repository.GetEntryExitTotalsAsync(yesterdayStart, todayStart, ct);
        var inside = await repository.GetCurrentlyInsideAsync(ct);
        var recent = await repository.GetRecentActivityAsync(8, ct);

        return new DashboardOverviewResponse(
            accounts, vehicles, pending,
            new TodayOperationsDto(today.Entries, today.Exits, yesterday.Entries, yesterday.Exits, inside),
            recent, now);
    }
}

/// <summary>Philippine-time day arithmetic shared by the dashboard handlers.</summary>
public static class DashboardTime
{
    /// <summary>UTC bounds of the Philippine calendar day <paramref name="dayOffset"/> days from today.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) DayBoundsUtc(DateTime utcNow, int dayOffset)
    {
        var localDay = ParkingTimeHelper.ConvertUtcToPhilippinesTime(utcNow).Date.AddDays(dayOffset);
        var start = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(localDay, TimeSpan.Zero);
        var end = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(localDay.AddDays(1), TimeSpan.Zero);
        return (start, end);
    }
}
