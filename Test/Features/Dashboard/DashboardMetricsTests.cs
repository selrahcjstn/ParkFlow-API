using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ParkFlow.Application.Features.Dashboard.Queries.GetDashboardSummary;
using ParkFlow.Application.Interfaces;
using ParkFlow.Persistence;
using ParkFlow.Persistence.Repositories;
using Xunit;

namespace Test.Features.Dashboard;

public class DashboardMetricsTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(90, 80, 12.5)]
    [InlineData(200, 100, 100)]
    [InlineData(0, 100, -100)]
    [InlineData(29, 30, -3.3)]
    [InlineData(1, 3, -66.7)]
    public void PercentagesAreCalculatedAndRoundedToOneDecimal(decimal current, decimal previous, decimal expected)
        => Assert.Equal(expected, GetDashboardSummaryHandler.CalculatePercentageChange(current, previous));

    [Fact]
    public void NonzeroAgainstZeroHasNoFinitePercentage()
        => Assert.Null(GetDashboardSummaryHandler.CalculatePercentageChange(20m, 0m));

    [Fact]
    public async Task SummaryUsesActualMetricsAndDailyBaselines()
    {
        var handler = new GetDashboardSummaryHandler(new FakeDashboardRepository());
        var result = await handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Data!.ActiveParking);
        Assert.Equal(12.5m, result.Data.Trends!.RegisteredClientsPercent);
        Assert.Equal(new DateTime(2026, 10, 11), result.Data.Trends.ComparisonDate);
    }


    [Fact]
    public async Task OccupancyIncludesVisitorsWithoutTruncatingRegisteredCount()
    {
        var capture = new QueryCapture(1001, 4);
        await using var db = Context(capture);
        Assert.Equal(1005, await new DashboardRepository(db).GetActiveParkingCountAsync());
        Assert.Equal(2, capture.Commands.Count);
        Assert.Contains("\"ParkingLogs\"", capture.Commands[0].Sql);
        foreach (var query in capture.Commands)
        {
            Assert.Contains("\"ExitTime\" IS NULL", query.Sql);
            Assert.Contains("\"Status\" = 0", query.Sql);
            Assert.DoesNotContain("LIMIT", query.Sql);
        }
    }

    [Fact]
    public async Task NotificationUnreadCountExcludesRetiredFinancialMessagesInTheDatabaseQuery()
    {
        var capture = new QueryCapture(0);
        await using var db = Context(capture);
        Assert.Equal(0, await new UserNotificationRepository(db).GetUnreadCountAsync(Guid.NewGuid()));
        var query = Assert.Single(capture.Commands).Sql;
        foreach (var field in new[] { "Type", "ActionRoute", "ReferenceCode", "Title", "Body", "IsRead", "UserAccountId" })
            Assert.Contains($"\"{field}\"", query);
        Assert.Contains("process-payment", query);
        Assert.Contains("VIO-%", query);
        Assert.Contains("penalty", query);
        Assert.DoesNotContain("\"Violations\"", query);
    }


    [Fact]
    public async Task DailyComparisonUsesPriorManilaDayAndOnlyOperationalClients()
    {
        var capture = new QueryCapture(80);
        await using var db = Context(capture);
        var comparison = await new DashboardRepository(db).GetDailyComparisonAsync(
            new DateTime(2026, 10, 11, 17, 0, 0, DateTimeKind.Utc));
        Assert.Equal(80, comparison.RegisteredClients);
        Assert.Single(capture.Commands);
        Assert.DoesNotContain("Violations", capture.Commands[0].Sql);
        Assert.Equal(new DateTime(2026, 10, 11), comparison.Date);
        Assert.Contains("\"CreatedAt\" <", capture.Commands[0].Sql);
        Assert.Contains("\"Admins\"", capture.Commands[0].Sql);
        Assert.Contains("\"Guards\"", capture.Commands[0].Sql);
    }


    private static AppDbContext Context(QueryCapture capture) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=not_used;Username=not_used;Password=not_used")
        .AddInterceptors(new SuppressConnection(), capture).Options);

    // Inspect the actual EF-generated queries without accessing or writing any database.
    private sealed class SuppressConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult.Suppress());
    }

    private sealed class QueryCapture(params object[] values) : DbCommandInterceptor
    {
        private readonly Queue<object> _values = new(values);
        public List<(string Sql, DateTime[] Dates)> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add((command.CommandText, command.Parameters.Cast<DbParameter>()
                .Where(p => p.Value is DateTime).Select(p => (DateTime)p.Value!).ToArray()));
            var value = _values.Dequeue();
            var table = new DataTable();
            table.Columns.Add("Value", value.GetType());
            table.Rows.Add(value);
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
        }
    }

    private sealed class FakeDashboardRepository : IDashboardRepository
    {
        public Task<int> GetTotalUsersCountAsync() => Task.FromResult(90);
        public Task<int> GetActiveParkingCountAsync() => Task.FromResult(12);
        public Task<DashboardDailyComparison> GetDailyComparisonAsync(DateTime asOfUtc)
            => Task.FromResult(new DashboardDailyComparison(80, new DateTime(2026, 10, 11)));
        public Task<Dictionary<DateTime, (int CheckIns, int CheckOuts)>> GetActivityOverLast7DaysAsync()
            => Task.FromResult(new Dictionary<DateTime, (int, int)>());
    }
}
