using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ParkFlow.Persistence;
using Xunit;

namespace Test.Features.Deployment;

public class DatabaseStartupSafetyTests
{
    private const string CompatibilityMigration = "20261027202102_RetireFinancialContractsPreserveHistory";

    [Fact]
    public async Task ReadyDatabaseIsCheckedWithSelectsOnly()
    {
        var capture = new HistoryCapture();
        using var db = Context(capture);
        capture.Applied = db.Database.GetMigrations().ToArray();
        await DatabaseStartupCheck.EnsureReadyAsync(db);
        Assert.NotEmpty(capture.Commands);
        Assert.All(capture.Commands, sql => Assert.StartsWith("SELECT", sql.TrimStart()));
    }

    [Fact]
    public async Task MissingMigrationStopsBeforeSeedingOrChangingSchema()
    {
        var capture = new HistoryCapture();
        using var db = Context(capture);
        capture.Applied = db.Database.GetMigrations().Where(id => id != CompatibilityMigration).ToArray();
        await using var services = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SuperAdminSeeder.SeedSuperAdminAsync(services));
        Assert.Contains(CompatibilityMigration, error.Message);
        Assert.Contains("docs/deployment-migration-safety.md", error.Message);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.All(capture.Commands, sql => Assert.StartsWith("SELECT", sql.TrimStart()));
    }

    [Theory]
    [InlineData(DatabaseStartupCheck.DeferredDeletionMigration)]
    [InlineData("20990101000000_UnknownRelease")]
    public async Task AppliedMigrationConflictsStopTheRelease(string unexpected)
    {
        var capture = new HistoryCapture();
        using var db = Context(capture);
        capture.Applied = db.Database.GetMigrations().Append(unexpected).ToArray();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseStartupCheck.EnsureReadyAsync(db));
        Assert.Contains(unexpected, error.Message);
        Assert.All(capture.Commands, sql => Assert.StartsWith("SELECT", sql.TrimStart()));
    }

    [Fact]
    public async Task ConnectionFailureIsActionableAndNeverRunsMigrations()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused")
            .AddInterceptors(new RefuseConnection()).Options);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseStartupCheck.EnsureReadyAsync(db));
        Assert.Contains("connectivity and permissions", error.Message);
        Assert.Contains("does not apply migrations", error.Message);
    }

    [Fact]
    public void ReleaseScriptPreservesFinancialStorageAndSnapshotMatches()
    {
        using var db = Context(new HistoryCapture());
        Assert.False(db.Database.HasPendingModelChanges());
        var migrations = db.Database.GetMigrations().ToArray();
        Assert.Contains(CompatibilityMigration, migrations);
        Assert.DoesNotContain(DatabaseStartupCheck.DeferredDeletionMigration, migrations);
        var sql = db.GetService<IMigrator>().GenerateScript("20261021104419_AddDriverDocumentsAndDurableSettings", CompatibilityMigration);
        Assert.DoesNotContain("DROP ", sql);
        Assert.DoesNotContain("DELETE FROM", sql);
        Assert.DoesNotContain("UPDATE ", sql);
        Assert.Contains("Expected historical financial storage is missing", sql);
        Assert.Contains(DatabaseStartupCheck.DeferredDeletionMigration, sql);
    }

    [Fact]
    public void NormalReleaseChainContainsNoDestructiveCleanup()
    {
        using var db = Context(new HistoryCapture());
        var assembly = db.GetService<IMigrationsAssembly>();
        foreach (var entry in assembly.Migrations.Where(m => string.CompareOrdinal(m.Key, "20261021104419_AddDriverDocumentsAndDurableSettings") > 0))
        {
            var migration = assembly.CreateMigration(entry.Value, db.Database.ProviderName!);
            Assert.All(migration.UpOperations, operation =>
            {
                Assert.False(operation.IsDestructiveChange);
                Assert.False(operation is DropTableOperation or DropColumnOperation or DeleteDataOperation or UpdateDataOperation);
                if (operation is SqlOperation sql)
                {
                    if (migration is ParkFlow.Persistence.Migrations.SeparateVisitorVehicles)
                    {
                        Assert.Contains(sql.Sql, new[] { ParkFlow.Persistence.Migrations.SeparateVisitorVehicles.PreflightSql,
                            ParkFlow.Persistence.Migrations.SeparateVisitorVehicles.BackfillSql });
                        Assert.DoesNotMatch(@"(?i)\b(DELETE|DROP|TRUNCATE)\b", sql.Sql);
                    }
                    else if (migration is ParkFlow.Persistence.Migrations.EnforceVehiclePlateOwnership)
                    {
                        Assert.Contains(sql.Sql, new[] { ParkFlow.Persistence.Migrations.EnforceVehiclePlateOwnership.PreflightSql,
                            ParkFlow.Persistence.Migrations.EnforceVehiclePlateOwnership.AuthoritySql });
                        Assert.DoesNotMatch(@"(?i)\b(DELETE\s+FROM|TRUNCATE\s+TABLE|DROP\s+(TABLE|COLUMN))\b", sql.Sql);
                        Assert.DoesNotMatch(@"(?i)UPDATE\s+""(Vehicles|VisitorVehicles|Visitors|VisitSessions|Violations|Feedbacks)""", sql.Sql);
                    }
                    else if (migration is ParkFlow.Persistence.Migrations.EnsureCorDocumentColumns)
                    {
                        Assert.Equal(ParkFlow.Persistence.Migrations.EnsureCorDocumentColumns.CompatibilitySql, sql.Sql);
                        Assert.DoesNotMatch(@"(?i)\b(DELETE|DROP|TRUNCATE|UPDATE|INSERT)\b", sql.Sql);
                    }
                    else Assert.DoesNotMatch(@"(?i)\b(DELETE|DROP|TRUNCATE|UPDATE|INSERT)\b", sql.Sql);
                }
            });
        }
    }

    [Fact]
    public async Task MatchingHistoryCannotHideMissingModelColumns()
    {
        var capture = new HistoryCapture(); using var db = Context(capture);
        capture.Applied = db.Database.GetMigrations().ToArray();
        capture.Columns = capture.Columns.Where(c => c != "CorSubmissions.MotorPictureUrl").ToArray();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseStartupCheck.EnsureReadyAsync(db));
        Assert.Contains("CorSubmissions.MotorPictureUrl", error.Message);
        Assert.All(capture.Commands, sql => Assert.StartsWith("SELECT", sql.TrimStart()));
    }

    private static AppDbContext Context(HistoryCapture capture)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused;Username=unused")
            .AddInterceptors(new SuppressConnection(), capture).Options);
        capture.Columns = db.Model.GetRelationalModel().Tables.SelectMany(t => t.Columns.Select(c => t.Name + "." + c.Name)).ToArray();
        return db;
    }

    private sealed class SuppressConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult.Suppress());
    }

    private sealed class RefuseConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new NpgsqlException("Database is unavailable in this test.");
    }

    private sealed class HistoryCapture : DbCommandInterceptor
    {
        public string[] Applied { get; set; } = [];
        public string[] Columns { get; set; } = [];
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            Assert.StartsWith("SELECT", command.CommandText.TrimStart());
            var table = new DataTable();
            if (command.CommandText.Contains("information_schema.columns"))
            {
                table.Columns.Add("Value", typeof(string)); foreach (var column in Columns) table.Rows.Add(column);
                return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
            }
            table.Columns.Add("MigrationId", typeof(string));
            table.Columns.Add("ProductVersion", typeof(string));
            foreach (var id in Applied) table.Rows.Add(id, "10.0.7");
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Startup attempted a database write: " + command.CommandText);
    }
}
