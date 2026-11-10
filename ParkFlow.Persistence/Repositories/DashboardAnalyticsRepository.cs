using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Features.Dashboard.Queries.GetDashboardAnalytics;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Persistence.Repositories;

/// <summary>
/// Dashboard aggregations. Every count/group runs in the database; only small result sets
/// (buckets, the latest few transactions) are materialised.
/// </summary>
public class DashboardAnalyticsRepository(AppDbContext db) : IDashboardAnalyticsRepository
{
    private IQueryable<UserAccount> ClientAccounts => db.UserAccounts.AsNoTracking().Where(u =>
        u.UserProfile == null || (u.UserProfile.Admin == null && u.UserProfile.Guard == null));

    // Asia/Manila has a fixed UTC+8 offset (no DST); read it from the zone instead of hardcoding.
    public static double PhilippinesOffsetHours() =>
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila").BaseUtcOffset.TotalHours;

    public async Task<AccountBreakdownDto> GetAccountBreakdownAsync(CancellationToken ct)
    {
        var byStatus = await ClientAccounts.GroupBy(u => u.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        int Of(AccountStatus s) => byStatus.Where(x => x.Status == s).Sum(x => x.Count);
        return new AccountBreakdownDto(
            Total: byStatus.Sum(x => x.Count),
            Students: await db.Students.CountAsync(ct),
            Personnel: await db.Personnel.CountAsync(ct),
            Guards: await db.Guards.CountAsync(ct),
            Admins: await db.Admins.CountAsync(ct),
            Active: Of(AccountStatus.Active),
            PendingVerification: Of(AccountStatus.PendingVerification),
            Suspended: Of(AccountStatus.Suspended));
    }

    public async Task<VehicleBreakdownDto> GetVehicleBreakdownAsync(CancellationToken ct)
    {
        var byType = await db.Vehicles.AsNoTracking().GroupBy(v => v.VehicleType)
            .Select(g => new { Type = g.Key, Count = g.Count() }).ToListAsync(ct);
        var byStatus = await db.Vehicles.AsNoTracking().GroupBy(v => v.VerificationStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        int Type(VehicleType t) => byType.Where(x => x.Type == t).Sum(x => x.Count);
        var approved = byStatus.Where(x => x.Status == CorVerificationStatus.Verified).Sum(x => x.Count);
        var rejected = byStatus.Where(x => x.Status == CorVerificationStatus.Rejected).Sum(x => x.Count);
        var total = byStatus.Sum(x => x.Count);
        return new VehicleBreakdownDto(total, Type(VehicleType.Motorcycle), Type(VehicleType.Car), Type(VehicleType.ElectricBike),
            Pending: total - approved - rejected, Approved: approved, Rejected: rejected);
    }

    public async Task<PendingWorkDto> GetPendingWorkAsync(CancellationToken ct)
    {
        var accounts = await ClientAccounts.CountAsync(u => u.Status == AccountStatus.PendingVerification, ct);
        var vehicles = await db.Vehicles.AsNoTracking().CountAsync(v =>
            v.VerificationStatus != CorVerificationStatus.Verified && v.VerificationStatus != CorVerificationStatus.Rejected, ct);
        // Only personnel schedules need an admin decision; student COR/schedules are automatic.
        var cor = await db.CorSubmissions.AsNoTracking().CountAsync(c =>
            c.UserAccount.UserProfile != null && c.UserAccount.UserProfile.Personnel != null &&
            c.VerificationStatus != CorVerificationStatus.Verified && c.VerificationStatus != CorVerificationStatus.Rejected, ct);
        return new PendingWorkDto(accounts, vehicles, cor);
    }

    public async Task<(int Entries, int Exits)> GetEntryExitTotalsAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        var entries = await db.ParkingLogs.CountAsync(l => l.EntryTime >= startUtc && l.EntryTime < endUtc, ct)
                      + await db.VisitSessions.CountAsync(s => s.EntryTime >= startUtc && s.EntryTime < endUtc, ct);
        var exits = await db.ParkingLogs.CountAsync(l => l.ExitTime != null && l.ExitTime >= startUtc && l.ExitTime < endUtc, ct)
                    + await db.VisitSessions.CountAsync(s => s.ExitTime != null && s.ExitTime >= startUtc && s.ExitTime < endUtc, ct);
        return (entries, exits);
    }

    public async Task<int> GetCurrentlyInsideAsync(CancellationToken ct) =>
        await db.ParkingLogs.CountAsync(l => l.Status == ParkingStatus.Parked && l.ExitTime == null, ct)
        + await db.VisitSessions.CountAsync(s => s.Status == VisitSessionStatus.Inside && s.ExitTime == null, ct);

    public IQueryable<DateTime> EntryTimes(DateTime startUtc, DateTime endUtc, bool visitors) => visitors
        ? db.VisitSessions.AsNoTracking().Where(s => s.EntryTime >= startUtc && s.EntryTime < endUtc).Select(s => s.EntryTime)
        : db.ParkingLogs.AsNoTracking().Where(l => l.EntryTime >= startUtc && l.EntryTime < endUtc).Select(l => l.EntryTime);

    public IQueryable<DateTime> ExitTimes(DateTime startUtc, DateTime endUtc, bool visitors) => visitors
        ? db.VisitSessions.AsNoTracking().Where(s => s.ExitTime != null && s.ExitTime >= startUtc && s.ExitTime < endUtc).Select(s => s.ExitTime!.Value)
        : db.ParkingLogs.AsNoTracking().Where(l => l.ExitTime != null && l.ExitTime >= startUtc && l.ExitTime < endUtc).Select(l => l.ExitTime!.Value);

    /// <summary>Groups instants by Philippine day or hour in SQL: shift by the zone offset, then truncate.</summary>
    public static IQueryable<KeyCount> GroupByLocalDay(IQueryable<DateTime> times, double offsetHours) =>
        times.GroupBy(t => t.AddHours(offsetHours).Date).Select(g => new KeyCount(g.Key, g.Count()));

    public static IQueryable<HourKeyCount> GroupByLocalHour(IQueryable<DateTime> times, double offsetHours) =>
        times.GroupBy(t => t.AddHours(offsetHours).Hour).Select(g => new HourKeyCount(g.Key, g.Count()));

    public sealed record KeyCount(DateTime Key, int Count);
    public sealed record HourKeyCount(int Key, int Count);

    public async Task<List<ActivityCountRow>> GetActivityBucketsAsync(DateTime startUtc, DateTime endUtc, bool hourly, CancellationToken ct)
    {
        var offset = PhilippinesOffsetHours();
        var localDay = startUtc.AddHours(offset).Date;
        var entries = new Dictionary<DateTime, int>();
        var exits = new Dictionary<DateTime, int>();

        foreach (var visitors in new[] { false, true })
        {
            if (hourly)
            {
                foreach (var row in await GroupByLocalHour(EntryTimes(startUtc, endUtc, visitors), offset).ToListAsync(ct))
                    Add(entries, localDay.AddHours(row.Key), row.Count);
                foreach (var row in await GroupByLocalHour(ExitTimes(startUtc, endUtc, visitors), offset).ToListAsync(ct))
                    Add(exits, localDay.AddHours(row.Key), row.Count);
            }
            else
            {
                foreach (var row in await GroupByLocalDay(EntryTimes(startUtc, endUtc, visitors), offset).ToListAsync(ct))
                    Add(entries, row.Key.Date, row.Count);
                foreach (var row in await GroupByLocalDay(ExitTimes(startUtc, endUtc, visitors), offset).ToListAsync(ct))
                    Add(exits, row.Key.Date, row.Count);
            }
        }

        return entries.Keys.Union(exits.Keys).OrderBy(k => k)
            .Select(k => new ActivityCountRow(k, entries.GetValueOrDefault(k), exits.GetValueOrDefault(k))).ToList();
    }

    public async Task<List<HourCountRow>> GetHourOfDayAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        var offset = PhilippinesOffsetHours();
        var entries = new Dictionary<int, int>();
        var exits = new Dictionary<int, int>();
        foreach (var visitors in new[] { false, true })
        {
            foreach (var row in await GroupByLocalHour(EntryTimes(startUtc, endUtc, visitors), offset).ToListAsync(ct))
                entries[row.Key] = entries.GetValueOrDefault(row.Key) + row.Count;
            foreach (var row in await GroupByLocalHour(ExitTimes(startUtc, endUtc, visitors), offset).ToListAsync(ct))
                exits[row.Key] = exits.GetValueOrDefault(row.Key) + row.Count;
        }
        return entries.Keys.Union(exits.Keys).OrderBy(h => h)
            .Select(h => new HourCountRow(h, entries.GetValueOrDefault(h), exits.GetValueOrDefault(h))).ToList();
    }

    public async Task<List<RecentParkingActivityDto>> GetRecentActivityAsync(int take, CancellationToken ct)
    {
        var registered = await db.ParkingLogs.AsNoTracking()
            .OrderByDescending(l => l.ExitTime ?? l.EntryTime).Take(take)
            .Select(l => new
            {
                l.Vehicle.PlateNumber,
                l.Vehicle.VehicleType,
                Owner = l.Vehicle.Owner.UserProfile != null
                    ? l.Vehicle.Owner.UserProfile.FirstName + " " + l.Vehicle.Owner.UserProfile.LastName : null,
                l.EntryTime,
                l.ExitTime,
            }).ToListAsync(ct);
        var visitors = await db.VisitSessions.AsNoTracking()
            .OrderByDescending(s => s.ExitTime ?? s.EntryTime).Take(take)
            .Select(s => new
            {
                s.VisitorVehicle.PlateNumber,
                s.VisitorVehicle.VehicleType,
                Owner = s.Visitor.FullName,
                s.EntryTime,
                s.ExitTime,
            }).ToListAsync(ct);

        static string TypeName(VehicleType t) => t == VehicleType.ElectricBike ? "E-Bike" : t.ToString();
        static string Identifier(string? plate, VehicleType t) =>
            !string.IsNullOrWhiteSpace(plate) ? plate : (t == VehicleType.ElectricBike ? "E-Bike (no plate)" : "—");

        return registered.Select(r => new RecentParkingActivityDto("Registered", Identifier(r.PlateNumber, r.VehicleType),
                r.Owner?.Trim() is { Length: > 0 } o ? o : "Registered user", TypeName(r.VehicleType), r.EntryTime, r.ExitTime,
                r.ExitTime == null ? "Inside" : "Exited"))
            .Concat(visitors.Select(v => new RecentParkingActivityDto("Visitor", Identifier(v.PlateNumber, v.VehicleType),
                string.IsNullOrWhiteSpace(v.Owner) ? "Visitor" : v.Owner, TypeName(v.VehicleType), v.EntryTime, v.ExitTime,
                v.ExitTime == null ? "Inside" : "Exited")))
            .OrderByDescending(a => a.ExitTime ?? a.EntryTime)
            .Take(take).ToList();
    }

    private static void Add(Dictionary<DateTime, int> map, DateTime key, int count) =>
        map[key] = map.GetValueOrDefault(key) + count;
}
