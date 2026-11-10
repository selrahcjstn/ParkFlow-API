using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Dashboard.Queries.GetDashboardAnalytics;
using ParkFlow.Application.Interfaces;
using ParkFlow.Persistence;
using ParkFlow.Persistence.Repositories;
using Xunit;

namespace Test.Features.Dashboard;

public class DashboardAnalyticsTests
{
    private sealed class FakeAnalyticsRepository : IDashboardAnalyticsRepository
    {
        public List<ActivityCountRow> Buckets { get; set; } = [];
        public List<HourCountRow> Hours { get; set; } = [];
        public (DateTime Start, DateTime End)? LastBucketWindow { get; private set; }
        public (DateTime Start, DateTime End)? LastHourWindow { get; private set; }
        public bool? LastHourly { get; private set; }
        public List<(DateTime Start, DateTime End)> TotalsWindows { get; } = [];

        public Task<AccountBreakdownDto> GetAccountBreakdownAsync(CancellationToken ct) =>
            Task.FromResult(new AccountBreakdownDto(10, 6, 2, 1, 1, 7, 2, 1));
        public Task<VehicleBreakdownDto> GetVehicleBreakdownAsync(CancellationToken ct) =>
            Task.FromResult(new VehicleBreakdownDto(9, 4, 3, 2, 3, 5, 1));
        public Task<PendingWorkDto> GetPendingWorkAsync(CancellationToken ct) =>
            Task.FromResult(new PendingWorkDto(2, 3, 1));
        public Task<(int Entries, int Exits)> GetEntryExitTotalsAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct)
        {
            TotalsWindows.Add((startUtc, endUtc));
            return Task.FromResult(TotalsWindows.Count == 1 ? (12, 9) : (8, 8));
        }
        public Task<int> GetCurrentlyInsideAsync(CancellationToken ct) => Task.FromResult(3);
        public Task<List<ActivityCountRow>> GetActivityBucketsAsync(DateTime startUtc, DateTime endUtc, bool hourly, CancellationToken ct)
        {
            LastBucketWindow = (startUtc, endUtc); LastHourly = hourly;
            return Task.FromResult(Buckets);
        }
        public Task<List<HourCountRow>> GetHourOfDayAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct)
        {
            LastHourWindow = (startUtc, endUtc);
            return Task.FromResult(Hours);
        }
        public Task<List<RecentParkingActivityDto>> GetRecentActivityAsync(int take, CancellationToken ct) =>
            Task.FromResult(new List<RecentParkingActivityDto>());
    }

    [Fact]
    public async Task Overview_CombinesRepositoryCountsWithoutInventingValues()
    {
        var repo = new FakeAnalyticsRepository();
        var result = await new GetDashboardOverviewHandler(repo).Handle(new GetDashboardOverviewQuery(), default);

        Assert.True(result.IsSuccess);
        var data = result.Data!;
        Assert.Equal(10, data.Accounts.Total);
        Assert.Equal(5, data.Vehicles.Approved);
        Assert.Equal(12, data.Today.Entries);
        Assert.Equal(8, data.Today.YesterdayEntries);
        Assert.Equal(3, data.Today.CurrentlyInside);
        // Today is a 24h Philippine day and yesterday ends exactly where today starts.
        Assert.Equal(TimeSpan.FromHours(24), repo.TotalsWindows[0].End - repo.TotalsWindows[0].Start);
        Assert.Equal(repo.TotalsWindows[0].Start, repo.TotalsWindows[1].End);
    }

    [Fact]
    public async Task Activity_Today_ReturnsAll24HoursZeroFilled()
    {
        var repo = new FakeAnalyticsRepository();
        var result = await new GetDashboardActivityHandler(repo).Handle(new GetDashboardActivityQuery(ActivityRange.Today), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(24, result.Data!.Series.Count);
        Assert.Equal("hour", result.Data.Granularity);
        Assert.True(repo.LastHourly);
        Assert.All(result.Data.Series, b => Assert.Equal(0, b.Entries + b.Exits));
        Assert.Equal(24, result.Data.PeakHours.Count);
        // Looking back a week for peak hours when only "today" is selected.
        Assert.Equal(7, result.Data.PeakWindowDays);
    }

    [Theory]
    [InlineData(ActivityRange.Last7Days, 7)]
    [InlineData(ActivityRange.Last30Days, 30)]
    public async Task Activity_Daily_ReturnsOneBucketPerPhilippineDay(ActivityRange range, int days)
    {
        var repo = new FakeAnalyticsRepository();
        var result = await new GetDashboardActivityHandler(repo).Handle(new GetDashboardActivityQuery(range), default);

        Assert.Equal(days, result.Data!.Series.Count);
        Assert.Equal(days, (int)Math.Round((repo.LastBucketWindow!.Value.End - repo.LastBucketWindow.Value.Start).TotalDays));
        Assert.Equal(days, result.Data.PeakWindowDays);
        Assert.Equal(result.Data.Series.Select(s => s.Key).Distinct().Count(), days);
    }

    [Fact]
    public void BuildSeries_PlacesCountsInTheRightBucketAndSumsTotals()
    {
        var start = new DateTime(2026, 11, 3, 16, 0, 0, DateTimeKind.Utc); // 2026-11-04 00:00 Philippine time
        var rows = new List<ActivityCountRow>
        {
            new(new DateTime(2026, 11, 4), 5, 2),
            new(new DateTime(2026, 11, 6), 1, 4),
        };

        var series = GetDashboardActivityHandler.BuildSeries(rows, start, 7, hourly: false);

        Assert.Equal(7, series.Count);
        Assert.Equal("2026-11-04", series[0].Key);
        Assert.Equal(5, series[0].Entries);
        Assert.Equal(2, series[0].Exits);
        Assert.Equal(1, series[2].Entries);
        Assert.Equal(4, series[2].Exits);
        Assert.Equal(6, series.Sum(s => s.Entries));
        Assert.Equal(6, series.Sum(s => s.Exits));
    }

    [Fact]
    public void BuildPeakHours_ReturnsEveryHourWithTwelveHourLabels()
    {
        var hours = GetDashboardActivityHandler.BuildPeakHours([new HourCountRow(7, 12, 1), new HourCountRow(17, 3, 14)]);

        Assert.Equal(24, hours.Count);
        Assert.Equal("12 AM", hours[0].Label);
        Assert.Equal("12 PM", hours[12].Label);
        Assert.Equal(12, hours[7].Entries);
        Assert.Equal(14, hours[17].Exits);
        Assert.Equal(0, hours[3].Entries);
    }

    [Fact]
    public async Task Activity_RejectsUnknownRange()
    {
        var result = await new GetDashboardActivityHandler(new FakeAnalyticsRepository())
            .Handle(new GetDashboardActivityQuery((ActivityRange)99), default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
    }

    [Fact]
    public void LocalDayAndHourGrouping_TranslateToDatabaseGroupBy()
    {
        // ToQueryString never opens a connection, so this verifies EF can translate the
        // Philippine-time bucketing to SQL (no client-side evaluation of whole tables).
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=parkflow_translation_check").Options);
        var repo = new DashboardAnalyticsRepository(db);
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;

        foreach (var visitors in new[] { false, true })
        {
            var daily = DashboardAnalyticsRepository.GroupByLocalDay(repo.EntryTimes(start, end, visitors), 8).ToQueryString();
            var hourly = DashboardAnalyticsRepository.GroupByLocalHour(repo.ExitTimes(start, end, visitors), 8).ToQueryString();
            Assert.Contains("GROUP BY", daily, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("GROUP BY", hourly, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("COUNT(*)", daily, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(8, DashboardAnalyticsRepository.PhilippinesOffsetHours());
    }
}
