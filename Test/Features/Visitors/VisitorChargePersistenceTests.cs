using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using ParkFlow.Domain.Entities;
using ParkFlow.Persistence;
using ParkFlow.Persistence.Repositories;
using Xunit;

namespace Test.Features.Visitors;

public class VisitorChargePersistenceTests
{
    private static DbContextOptions<AppDbContext> Options() => new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options;

    [Fact]
    public void ChargeModelAndMigrationSupportEitherSession_AndUniqueVisitorCharges()
    {
        using var context = new AppDbContext(Options());
        var charge = context.Model.FindEntityType(typeof(Violation))!;
        Assert.True(charge.FindProperty(nameof(Violation.ParkingLogId))!.IsNullable);
        Assert.False(charge.GetForeignKeys().Single(f => f.Properties.Any(p => p.Name == nameof(Violation.ParkingLogId))).IsRequired);
        Assert.True(charge.GetIndexes().Single(i => i.Properties.Any(p => p.Name == nameof(Violation.VisitSessionId))).IsUnique);
        Assert.True(context.Model.FindEntityType(typeof(VisitSession))!.FindProperty(nameof(VisitSession.Status))!.IsConcurrencyToken);
        Assert.Contains("20261010090000_AddVisitorParkingCharges", context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges());
        var migration = new ParkFlow.Persistence.Migrations.AddVisitorParkingCharges();
        Assert.Contains(migration.UpOperations, o => o is AddCheckConstraintOperation { Name: "CK_Violations_Session" });
        Assert.Contains(migration.UpOperations, o => o is AddForeignKeyOperation { PrincipalTable: "VisitSessions" });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExitAndChargeAreSavedTogether_ConcurrentExitIsRejected(bool conflicting)
    {
        using var context = new RecordingContext(Options()) { Conflicting = conflicting };
        var session = new VisitSession(Guid.NewGuid());
        context.Attach(session);
        session.MarkExit();
        var charge = Violation.CreateVisitorCharge(session);
        var result = await new VisitorRepository(context).CompleteVisitSessionWithChargeAsync(session, charge);
        Assert.Equal(!conflicting, result);
        Assert.Equal(1, context.SaveCount);
        Assert.True(context.SawExitAndCharge);
    }

    [Fact]
    public async Task DuplicateChargeConstraintIsHandled_ButOtherDatabaseErrorsAreNotHidden()
    {
        var session = new VisitSession(Guid.NewGuid());
        session.MarkExit();
        using var duplicate = new RecordingContext(Options()) { DuplicateCharge = true };
        Assert.False(await new VisitorRepository(duplicate).CompleteVisitSessionWithChargeAsync(session, Violation.CreateVisitorCharge(session)));
        using var unavailable = new RecordingContext(Options()) { DatabaseUnavailable = true };
        await Assert.ThrowsAsync<DbUpdateException>(() => new VisitorRepository(unavailable)
            .CompleteVisitSessionWithChargeAsync(session, Violation.CreateVisitorCharge(session)));
    }

    private sealed class RecordingContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        public bool Conflicting { get; init; }
        public bool DuplicateCharge { get; init; }
        public bool DatabaseUnavailable { get; init; }
        public int SaveCount { get; private set; }
        public bool SawExitAndCharge { get; private set; }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            var session = Assert.Single(ChangeTracker.Entries<VisitSession>());
            var charge = Assert.Single(ChangeTracker.Entries<Violation>());
            SawExitAndCharge = session.State == EntityState.Modified && charge.State == EntityState.Added &&
                session.Property(s => s.Status).OriginalValue == VisitSessionStatus.Inside &&
                session.Entity.Status == VisitSessionStatus.Completed && charge.Entity.PenaltyFee == 20m;
            if (Conflicting) throw new DbUpdateConcurrencyException("Another guard already recorded exit.");
            if (DuplicateCharge) throw new DbUpdateException("Duplicate charge", new PostgresException(
                "Charge already exists", "ERROR", "ERROR", "23505", constraintName: "IX_Violations_VisitSessionId"));
            if (DatabaseUnavailable) throw new DbUpdateException("Database unavailable");
            return Task.FromResult(2);
        }
    }
}
