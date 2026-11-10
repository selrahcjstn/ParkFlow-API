using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Dashboard.Queries.GetDashboardAnalytics;

public record GetDashboardActivityQuery(ActivityRange Range = ActivityRange.Last7Days)
    : IRequest<Result<DashboardActivityResponse>>;

public class GetDashboardActivityHandler(IDashboardAnalyticsRepository repository, ICacheService? cache = null)
    : IRequestHandler<GetDashboardActivityQuery, Result<DashboardActivityResponse>>
{
    private const string Timezone = "Asia/Manila";

    public async Task<Result<DashboardActivityResponse>> Handle(GetDashboardActivityQuery request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Range))
            return Result<DashboardActivityResponse>.Failure("Choose today, 7d or 30d.", ErrorCode.BadRequest);
        try
        {
            var key = CacheKeys.DashboardActivity(request.Range.ToString());
            var response = cache == null
                ? await LoadAsync(request.Range, DateTime.UtcNow, ct)
                : await cache.GetOrCreateAsync(key, () => LoadAsync(request.Range, DateTime.UtcNow, ct), TimeSpan.FromSeconds(30), ct);
            return Result<DashboardActivityResponse>.Success(response!, "Dashboard activity retrieved.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<DashboardActivityResponse>.Failure($"Could not load dashboard activity: {ex.Message}", ErrorCode.ServerError);
        }
    }

    private async Task<DashboardActivityResponse> LoadAsync(ActivityRange range, DateTime nowUtc, CancellationToken ct)
    {
        var days = range switch { ActivityRange.Today => 1, ActivityRange.Last7Days => 7, _ => 30 };
        var hourly = range == ActivityRange.Today;
        var (startUtc, _) = DashboardTime.DayBoundsUtc(nowUtc, -(days - 1));
        var (_, endUtc) = DashboardTime.DayBoundsUtc(nowUtc, 0);

        var rows = await repository.GetActivityBucketsAsync(startUtc, endUtc, hourly, ct);
        var series = BuildSeries(rows, startUtc, days, hourly);

        // A single day says little about "busy hours", so Today looks back a week.
        var peakDays = range == ActivityRange.Today ? 7 : days;
        var (peakStart, _) = DashboardTime.DayBoundsUtc(nowUtc, -(peakDays - 1));
        var hourRows = await repository.GetHourOfDayAsync(peakStart, endUtc, ct);

        return new DashboardActivityResponse(
            Range: range switch { ActivityRange.Today => "today", ActivityRange.Last7Days => "7d", _ => "30d" },
            Granularity: hourly ? "hour" : "day",
            Timezone: Timezone,
            StartUtc: startUtc,
            EndUtc: endUtc,
            PeakWindowDays: peakDays,
            TotalEntries: series.Sum(s => s.Entries),
            TotalExits: series.Sum(s => s.Exits),
            Series: series,
            PeakHours: BuildPeakHours(hourRows));
    }

    /// <summary>Fills every bucket in the window so charts never skip an empty hour or day.</summary>
    public static List<ActivityBucketDto> BuildSeries(IEnumerable<ActivityCountRow> rows, DateTime startUtc, int days, bool hourly)
    {
        var localStart = ParkingTimeHelper.ConvertUtcToPhilippinesTime(startUtc);
        var lookup = rows.ToDictionary(r => r.LocalBucket, r => r);
        var result = new List<ActivityBucketDto>();
        if (hourly)
        {
            for (var h = 0; h < 24; h++)
            {
                var bucket = localStart.AddHours(h);
                lookup.TryGetValue(bucket, out var row);
                result.Add(new ActivityBucketDto(bucket.ToString("yyyy-MM-ddTHH:00", CultureInfo.InvariantCulture),
                    HourLabel(h), row?.Entries ?? 0, row?.Exits ?? 0));
            }
            return result;
        }
        for (var d = 0; d < days; d++)
        {
            var bucket = localStart.Date.AddDays(d);
            lookup.TryGetValue(bucket, out var row);
            result.Add(new ActivityBucketDto(bucket.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                days <= 7 ? bucket.ToString("ddd d", CultureInfo.InvariantCulture) : bucket.ToString("MMM d", CultureInfo.InvariantCulture),
                row?.Entries ?? 0, row?.Exits ?? 0));
        }
        return result;
    }

    public static List<HourOfDayDto> BuildPeakHours(IEnumerable<HourCountRow> rows)
    {
        var lookup = rows.ToDictionary(r => r.Hour);
        return Enumerable.Range(0, 24).Select(h =>
        {
            lookup.TryGetValue(h, out var row);
            return new HourOfDayDto(h, HourLabel(h), row?.Entries ?? 0, row?.Exits ?? 0);
        }).ToList();
    }

    private static string HourLabel(int hour)
    {
        var suffix = hour < 12 ? "AM" : "PM";
        var twelve = hour % 12 == 0 ? 12 : hour % 12;
        return $"{twelve} {suffix}";
    }
}
