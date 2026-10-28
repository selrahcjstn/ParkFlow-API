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

public class VisitorCheckoutPersistenceTests
{
    private static DbContextOptions<AppDbContext> Options() => new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options;



    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreeExitPersistsOperationalStateAndRejectsConcurrentExit(bool conflicting)
    {
        using var context = new RecordingContext(Options()) { Conflicting = conflicting };
        var session = new VisitSession(Guid.NewGuid()); context.Attach(session); session.MarkExit();
        var result = await new VisitorRepository(context).CompleteVisitSessionAsync(session);
        Assert.Equal(!conflicting, result); Assert.Equal(1, context.SaveCount); Assert.True(context.SawExit);
    }

    [Fact]
    public async Task DatabaseErrorsAreNotHidden()
    {
        var session = new VisitSession(Guid.NewGuid()); session.MarkExit();
        using var context = new RecordingContext(Options()) { DatabaseUnavailable = true };
        await Assert.ThrowsAsync<DbUpdateException>(() => new VisitorRepository(context).CompleteVisitSessionAsync(session));
    }

    private sealed class RecordingContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        public bool Conflicting { get; init; }
        public bool DatabaseUnavailable { get; init; }
        public int SaveCount { get; private set; }
        public bool SawExit { get; private set; }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            var session = Assert.Single(ChangeTracker.Entries<VisitSession>());
            SawExit = session.State == EntityState.Modified && session.Property(s => s.Status).OriginalValue == VisitSessionStatus.Inside
                && session.Entity.Status == VisitSessionStatus.Completed && session.Entity.ExitTime != null;
            if (Conflicting) throw new DbUpdateConcurrencyException("Another guard already recorded exit.");
            if (DatabaseUnavailable) throw new DbUpdateException("Database unavailable");
            return Task.FromResult(1);
        }
    }
}
