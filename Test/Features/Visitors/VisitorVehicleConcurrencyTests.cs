using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Persistence;
using ParkFlow.Persistence.Repositories;
using Xunit;
using Test.Support;

namespace Test.Features.Visitors;

// A disposable local database is required. The fixture migrates a fresh empty test
// schema only. Each run uses synthetic IDs/plates and retains its test history.
public sealed class VisitorPostgresFactAttribute : FactAttribute
{
    public VisitorPostgresFactAttribute()
    {
        var value = Environment.GetEnvironmentVariable("PARKFLOW_VISITOR_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(value)) { Skip = "Requires disposable local parkflow_visitor_test via PARKFLOW_VISITOR_TEST_CONNECTION."; return; }
        var target = new NpgsqlConnectionStringBuilder(value);
        if (target.Host is not ("localhost" or "127.0.0.1") || !(target.Database ?? "").EndsWith("_visitor_test", StringComparison.Ordinal))
            Skip = "Visitor concurrency tests refuse non-local or non-test databases.";
    }
}

[Collection("Parking deadline settings")]
public class VisitorVehicleConcurrencyTests(VisitorPostgresDatabase database) : IClassFixture<VisitorPostgresDatabase>
{
    private AppDbContext Context() => database.Context();
    private static (Visitor Person, VisitorVehicle Vehicle, VisitSession Session) Entry(string? plate = null, VehicleType type = VehicleType.Car, Visitor? person = null)
    {
        person ??= new Visitor("Concurrency test", "09171234567", plate ?? "T" + Guid.NewGuid().ToString("N")[..10], type, "Test")
        { DriversLicenseUrl = "test-license", ProfilePictureUrl = "test-profile" };
        var vehicle = new VisitorVehicle(person.Id, plate ?? person.PlateNumber, type, "Test") { VehiclePictureUrl = "test-vehicle" };
        var session = new VisitSession(person.Id) { VisitorVehicleId = vehicle.Id, DriversLicenseUrl = "snapshot-license", ProfilePictureUrl = "snapshot-profile", VehiclePictureUrl = "snapshot-vehicle" };
        return (person, vehicle, session);
    }
    private static async Task<bool> TryEntry(AppDbContext db, Visitor p, VisitorVehicle v, VisitSession s, bool newPerson, bool newVehicle)
    {
        try { await new VisitorRepository(db).SaveEntryAsync(p, v, s, newPerson, newVehicle); return true; }
        catch (ParkingCapacityException) { return false; }
    }
    [VisitorPostgresFact]
    public async Task ConcurrentFirstRegistrationCreatesOnlyOnePersonVehicleAndSession()
    {
        var plate = "T" + Guid.NewGuid().ToString("N")[..10];
        var first = Entry(plate); var second = Entry(plate);
        using var a = Context(); using var b = Context();
        var results = await Task.WhenAll(TryEntry(a, first.Person, first.Vehicle, first.Session, true, true), TryEntry(b, second.Person, second.Vehicle, second.Session, true, true));
        Assert.Single(results, x => x);
        using var check = Context();
        Assert.Equal(1, await check.VisitorVehicles.CountAsync(v => v.NormalizedPlateNumber == plate.ToUpperInvariant()));
        Assert.Equal(1, await check.Visitors.CountAsync(v => v.Id == first.Person.Id || v.Id == second.Person.Id));
    }
    [VisitorPostgresFact]
    public async Task ConcurrentReentryCreatesOneActiveSessionAndPreservesCompletedHistory()
    {
        var f = Entry();
        using (var setup = Context())
        {
            await new VisitorRepository(setup).SaveEntryAsync(f.Person, f.Vehicle, f.Session, true, true);
            f.Session.MarkExit(); Assert.True(await new VisitorRepository(setup).CompleteVisitSessionAsync(f.Session));
            await setup.Entry(f.Session).ReloadAsync();
        }
        using var a = Context(); using var b = Context();
        var va = await new VisitorRepository(a).GetVehicleByPlateNumberAsync(f.Vehicle.PlateNumber);
        var vb = await new VisitorRepository(b).GetVehicleByPlateNumberAsync(f.Vehicle.PlateNumber);
        var one = new VisitSession(f.Person.Id) { VisitorVehicleId = f.Vehicle.Id };
        var two = new VisitSession(f.Person.Id) { VisitorVehicleId = f.Vehicle.Id };
        var results = await Task.WhenAll(TryEntry(a, va!.Visitor, va, one, false, false), TryEntry(b, vb!.Visitor, vb, two, false, false));
        Assert.Single(results, x => x);
        using var check = Context();
        var history = await check.VisitSessions.SingleAsync(s => s.Id == f.Session.Id);
        Assert.Equal(f.Session.EntryTime, history.EntryTime); Assert.Equal(f.Session.ExitTime, history.ExitTime);
        Assert.Equal("snapshot-license", history.DriversLicenseUrl);
        Assert.Equal(1, await check.VisitSessions.CountAsync(s => s.VisitorVehicleId == f.Vehicle.Id && s.Status == VisitSessionStatus.Inside));
    }
    [VisitorPostgresFact]
    public async Task ConcurrentExitCompletesTheSelectedSessionOnlyOnce()
    {
        var f = Entry(); using (var setup = Context()) await new VisitorRepository(setup).SaveEntryAsync(f.Person, f.Vehicle, f.Session, true, true);
        using var a = Context(); using var b = Context();
        var one = await new VisitorRepository(a).GetActiveVisitSessionByVehicleIdAsync(f.Vehicle.Id);
        var two = await new VisitorRepository(b).GetActiveVisitSessionByVehicleIdAsync(f.Vehicle.Id);
        one!.MarkExit(); two!.MarkExit();
        var results = await Task.WhenAll(new VisitorRepository(a).CompleteVisitSessionAsync(one), new VisitorRepository(b).CompleteVisitSessionAsync(two));
        Assert.Single(results, x => x);
    }
    [VisitorPostgresFact]
    public async Task TwoVehiclesUseIndependentCarAndMotorcycleCapacity()
    {
        var previous = SystemSettingsStore.Current;
        try
        {
            using var db = Context();
            var settings = SystemSettingsStore.Current;
            settings.CarParking.TotalCapacity = await db.ParkingLogs.CountAsync(p => p.Status == ParkingStatus.Parked && p.ExitTime == null && p.Vehicle.VehicleType == VehicleType.Car)
                + await db.VisitSessions.CountAsync(s => s.Status == VisitSessionStatus.Inside && s.VisitorVehicle.VehicleType == VehicleType.Car) + 1;
            settings.MotorcycleParking.TotalCapacity = await db.ParkingLogs.CountAsync(p => p.Status == ParkingStatus.Parked && p.ExitTime == null && p.Vehicle.VehicleType != VehicleType.Car)
                + await db.VisitSessions.CountAsync(s => s.Status == VisitSessionStatus.Inside && s.VisitorVehicle.VehicleType != VehicleType.Car) + 1;
            SystemSettingsStore.Replace(settings);
            var first = Entry(); var second = Entry();
            using var a = Context(); using var b = Context();
            var results = await Task.WhenAll(TryEntry(a, first.Person, first.Vehicle, first.Session, true, true), TryEntry(b, second.Person, second.Vehicle, second.Session, true, true));
            Assert.Single(results, x => x);
            var car = results[0] ? first : second;
            var motor = Entry("M" + Guid.NewGuid().ToString("N")[..10], VehicleType.Motorcycle, car.Person);
            await new VisitorRepository(db).SaveEntryAsync(car.Person, motor.Vehicle, motor.Session, false, true);
            Assert.Equal(2, await db.VisitSessions.CountAsync(s => s.VisitorId == car.Person.Id && s.Status == VisitSessionStatus.Inside));
            var extra = Entry();
            Assert.False(await TryEntry(db, extra.Person, extra.Vehicle, extra.Session, true, true));
        }
        finally { SystemSettingsStore.Replace(previous); }
    }
}
