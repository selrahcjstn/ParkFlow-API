using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Visitors;
using Test.Support;
using Xunit;

namespace Test.Features.Vehicles;

public sealed class VisitorPostgresTheoryAttribute : TheoryAttribute
{
    public VisitorPostgresTheoryAttribute() => Skip = new VisitorPostgresFactAttribute().Skip;
}

[Collection("Parking deadline settings")]
public class DatabasePlateOwnershipTests(VisitorPostgresDatabase database) : IClassFixture<VisitorPostgresDatabase>
{
    private async Task<Guid> Owner()
    {
        await using var db = database.Context(); var user = new UserAccount("synthetic", "09170000000");
        db.Add(user); await db.SaveChangesAsync(); return user.Id;
    }
    private static string Plate() => ("DB" + Guid.NewGuid().ToString("N")[..10]).ToUpperInvariant();
    [VisitorPostgresTheory]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.RepeatableRead)]
    [InlineData(IsolationLevel.Serializable)]
    public async Task DirectNonCooperatingWritersCannotRaceAcrossTables(IsolationLevel isolation)
    {
        var owner = await Owner(); var plate = Plate();
        var visitor = new Visitor("Synthetic", "09170000000", plate, VehicleType.Car, "Synthetic");
        var vehicle = new Vehicle(owner, plate.Insert(3,"-"), "Synthetic", "hash", VehicleType.Car);
        var visitorVehicle = new VisitorVehicle(visitor.Id, plate.ToLowerInvariant(), VehicleType.Car, "Synthetic");
        using var barrier = new Barrier(2);
        async Task<bool> Insert(bool registered)
        {
            await using var db = database.Context(); await using var transaction = await db.Database.BeginTransactionAsync(isolation);
            if (registered) db.Add(vehicle); else { db.Add(visitor); db.Add(visitorVehicle); }
            // Both transactions establish a snapshot before either source write.
            await db.Vehicles.CountAsync();
            await Task.Run(() => Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(20))));
            try { await db.SaveChangesAsync(); await transaction.CommitAsync(); return true; }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" or "40001" }) { return false; }
            catch (PostgresException ex) when (ex.SqlState is "23505" or "40001") { return false; }
        }
        var results = await Task.WhenAll(Insert(true), Insert(false)); Assert.Single(results, ok => ok);
        await using var check = database.Context();
        Assert.Equal(1, await check.Set<VehiclePlateOwnership>().CountAsync(x => x.NormalizedPlateNumber == plate));
        Assert.Equal(results[1], await check.Visitors.AnyAsync(v => v.Id == visitor.Id));
        Assert.Equal(results[0], await check.Vehicles.AnyAsync(v => v.Id == vehicle.Id));
    }
    [VisitorPostgresFact]
    public async Task DirectPlateEditCannotClaimAnotherVehicleAndRollsBack()
    {
        var owner = await Owner(); var first = new Vehicle(owner, Plate(), "Synthetic", "hash", VehicleType.Car);
        var second = new Vehicle(owner, Plate(), "Synthetic", "hash", VehicleType.Car);
        await using var db = database.Context(); db.AddRange(first,second); await db.SaveChangesAsync();
        var original = second.PlateNumber;
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Vehicles\" SET \"PlateNumber\"={first.PlateNumber} WHERE \"Id\"={second.Id}"));
        Assert.Equal("23505",error.SqlState);
        Assert.Equal(original,(await db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == second.Id)).PlateNumber);
    }
    [VisitorPostgresFact]
    public async Task OwnershipCannotBeRemovedOrForgedWhileItsSourceExists()
    {
        var owner = await Owner(); var vehicle = new Vehicle(owner,Plate(),"Synthetic","hash",VehicleType.Car);
        await using var db = database.Context(); db.Add(vehicle); await db.SaveChangesAsync();
        var deletion = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"VehiclePlateOwnership\" WHERE \"VehicleId\"={vehicle.Id}"));
        Assert.Equal("23514",deletion.SqlState);
        var forgery = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"VehiclePlateOwnership\" SET \"NormalizedPlateNumber\"={Plate()} WHERE \"VehicleId\"={vehicle.Id}"));
        Assert.Equal("23514",forgery.SqlState);
        Assert.True(await db.Set<VehiclePlateOwnership>().AnyAsync(x => x.VehicleId == vehicle.Id && x.NormalizedPlateNumber == vehicle.PlateNumber));
    }
    [VisitorPostgresFact]
    public async Task MultipleEditsInOneTransactionValidateFinalStateWithoutChangingIds()
    {
        var owner = await Owner(); var vehicle = new Vehicle(owner,Plate(),"Synthetic","hash",VehicleType.Car);
        var last = Plate(); await using var db = database.Context();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.Add(vehicle); await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Vehicles\" SET \"PlateNumber\"={Plate()} WHERE \"Id\"={vehicle.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Vehicles\" SET \"PlateNumber\"={last} WHERE \"Id\"={vehicle.Id}");
            await transaction.CommitAsync();
        }
        Assert.Equal(last,(await db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == vehicle.Id)).PlateNumber);
        Assert.Equal(last,(await db.Set<VehiclePlateOwnership>().SingleAsync(x => x.VehicleId == vehicle.Id)).NormalizedPlateNumber);
    }
    [VisitorPostgresFact]
    public async Task UnknownOwnerForeignKeyAndOwnershipTruncationAreRejected()
    {
        await using var db = database.Context();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"VehiclePlateOwnership\" (\"NormalizedPlateNumber\",\"VehicleId\") VALUES ({Plate()},{Guid.NewGuid()})"));
        Assert.Equal("23503",error.SqlState);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"VehiclePlateOwnership\""));
    }
    [VisitorPostgresFact]
    public async Task DatabaseCanonicalizationMatchesTheApprovedApplicationNormalization()
    {
        var owner = await Owner(); var raw = "\t ab-" + Guid.NewGuid().ToString("N")[..8] + " \r\n";
        var vehicle = new Vehicle(owner,raw,"Synthetic","hash",VehicleType.Car);
        await using var db = database.Context(); db.Add(vehicle); await db.SaveChangesAsync();
        Assert.Equal(VisitorVehicle.NormalizePlate(raw),(await db.Set<VehiclePlateOwnership>().SingleAsync(x => x.VehicleId == vehicle.Id)).NormalizedPlateNumber);
    }
    [VisitorPostgresFact]
    public async Task LegacyConflictStopsMigrationWithoutMergingOrChangingExistingRecords()
    {
        await using var existing = database.Context();
        var target = new NpgsqlConnectionStringBuilder(existing.Database.GetConnectionString());
        if (target.Host is not ("localhost" or "127.0.0.1") || target.Database != "parkflow_visitor_test") throw new InvalidOperationException("Disposable local database required.");
        target.SearchPath = "parkflow_conflict_" + Guid.NewGuid().ToString("N");
        await using var db = new ParkFlow.Persistence.AppDbContext(new DbContextOptionsBuilder<ParkFlow.Persistence.AppDbContext>().UseNpgsql(target.ConnectionString).Options);
        var create = "CREATE SCHEMA \""+target.SearchPath+"\""; await db.Database.ExecuteSqlRawAsync(create);
        var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20261028094839_SeparateVisitorVehicles");
        var user = new UserAccount("synthetic","09170000000"); var plate=Plate();
        var registered = new Vehicle(user.Id,plate,"Synthetic","hash",VehicleType.Car);
        var visitor = new Visitor("Synthetic","09170000000",plate,VehicleType.Car,"Synthetic");
        var vehicle = new VisitorVehicle(visitor.Id,plate,VehicleType.Car,"Synthetic");
        db.AddRange(user,registered,visitor,vehicle); await db.SaveChangesAsync();
        var before = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var error = await Assert.ThrowsAsync<PostgresException>(()=>migrator.MigrateAsync());
        Assert.Equal("P0001",error.SqlState); Assert.Contains("backfill blocked",error.MessageText);
        Assert.Equal(before,(await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.True(await db.Vehicles.AnyAsync(v=>v.Id==registered.Id));
        Assert.True(await db.VisitorVehicles.AnyAsync(v=>v.Id==vehicle.Id&&v.VisitorId==visitor.Id));
        Assert.True(await db.Visitors.AnyAsync(v=>v.Id==visitor.Id));
    }
}
