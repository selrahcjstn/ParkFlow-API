using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Visitors.DTOs;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Persistence;
using ParkFlow.Persistence.Repositories;
using Xunit;

namespace Test.Features.Visitors;

[Collection("Parking deadline settings")]
public class VisitorVehicleCapacityTests
{
    [Theory]
    [InlineData(VehicleType.Car, 6, false)]
    [InlineData(VehicleType.Car, 5, true)]
    [InlineData(VehicleType.Motorcycle, 8, true)]
    public async Task CapacityUsesTheActualVehicleGroupAndSerializesTheLastSpace(VehicleType type, int capacity, bool full)
    {
        var previous = SystemSettingsStore.Current;
        var settings = SystemSettingsStore.Current;
        settings.CarParking.TotalCapacity = type == VehicleType.Car ? capacity : 100;
        settings.MotorcycleParking.TotalCapacity = type == VehicleType.Motorcycle ? capacity : 100;
        SystemSettingsStore.Replace(settings);
        try
        {
            var capture = new Counts();
            using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused;Username=unused")
                .AddInterceptors(new NoConnection(), capture).Options);
            var guard = typeof(VisitorRepository).Assembly.GetType("ParkFlow.Persistence.Repositories.ParkingCapacityGuard")!;
            var method = guard.GetMethod("EnsureSpaceAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            var task = (Task)method.Invoke(null, [db, type])!;
            if (full) await Assert.ThrowsAsync<ParkingCapacityException>(() => task); else await task;
            Assert.Contains(capture.Commands, c => c.Contains("pg_advisory_xact_lock"));
            var visits = capture.Commands.Where(c => c.Contains("FROM \"VisitSessions\"")).ToArray();
            Assert.Equal(2, visits.Length);
            Assert.All(visits, sql => { Assert.Contains("JOIN \"VisitorVehicles\"", sql); Assert.DoesNotContain("JOIN \"Visitors\"", sql); });
        }
        finally { SystemSettingsStore.Replace(previous); }
    }
    [Fact]
    public void VisitorDirectoryProjectionTranslatesWithVehicleOwnershipAndSessions()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
        var projection = (Expression<Func<Visitor, VisitorDto>>)typeof(VisitorRepository).GetField("Summary", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var sql = db.Visitors.OrderBy(v => v.Id).Take(10).Select(projection).ToQueryString();
        Assert.Contains("VisitorVehicles", sql); Assert.Contains("VisitorVehicleId", sql);
        Assert.Contains("LIMIT", sql);
    }
    private sealed class NoConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData data,
            InterceptionResult result, CancellationToken ct = default) => ValueTask.FromResult(InterceptionResult.Suppress());
    }
    private sealed class Counts : DbCommandInterceptor
    {
        // Registered cars=2, visitor cars=3; registered motorcycles=4, visitor motorcycles=5.
        private readonly Queue<int> results = new([2, 3, 4, 5]);
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        { Commands.Add(command.CommandText); Assert.Contains("pg_advisory_xact_lock", command.CommandText); return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0)); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            Commands.Add(command.CommandText);
            var table = new DataTable(); table.Columns.Add("count", typeof(int)); table.Rows.Add(results.Dequeue());
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
        }
    }
}
