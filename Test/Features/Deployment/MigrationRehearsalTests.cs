using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ParkFlow.Persistence;
using Test.Features.Visitors;
using Xunit;
using Xunit.Abstractions;

namespace Test.Features.Deployment;

public class MigrationRehearsalTests(ITestOutputHelper output)
{
    [VisitorPostgresFact]
    public async Task ExistingCompatibleCorColumnsAndDocumentReferencesArePreserved()
    {
        var target = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PARKFLOW_VISITOR_TEST_CONNECTION"));
        if (target.Host is not ("localhost" or "127.0.0.1") || target.Database != "parkflow_visitor_test") throw new InvalidOperationException("Disposable local rehearsal database required.");
        target.SearchPath = "parkflow_cor_compat_" + Guid.NewGuid().ToString("N");
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(target.ConnectionString).Options);
        var create = "CREATE SCHEMA \"" + target.SearchPath + "\""; await db.Database.ExecuteSqlRawAsync(create);
        var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20261028134540_EnforceVehiclePlateOwnership");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"CorSubmissions\" ADD COLUMN \"OrcrDocumentUrl\" text NULL, ADD COLUMN \"MotorPictureUrl\" text NULL");
        var owner = new UserAccount("synthetic", "09170000000");
        var cor = new CorSubmission(owner.Id,"2026-2027","synthetic/cor.pdf","synthetic/existing-orcr.pdf","synthetic/existing-photo.png");
        db.AddRange(owner,cor); await db.SaveChangesAsync(); await db.Entry(cor).ReloadAsync(); var created = cor.CreatedAt;
        await migrator.MigrateAsync(); db.ChangeTracker.Clear();
        var saved = await db.CorSubmissions.SingleAsync(c => c.Id == cor.Id);
        Assert.Equal("synthetic/existing-orcr.pdf",saved.OrcrDocumentUrl); Assert.Equal("synthetic/existing-photo.png",saved.MotorPictureUrl);
        Assert.Equal(created,saved.CreatedAt); Assert.Equal(owner.Id,saved.UserAccountId);
        await DatabaseStartupCheck.EnsureReadyAsync(db);
    }

    [VisitorPostgresFact]
    public async Task PendingMigrationsPreserveSyntheticHistoryAndEnforceVisitorOwnership()
    {
        output.WriteLine("Verification runtime: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        var target = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PARKFLOW_VISITOR_TEST_CONNECTION"));
        if (target.Host is not ("localhost" or "127.0.0.1") || target.Database != "parkflow_visitor_test")
            throw new InvalidOperationException("Rehearsal requires local disposable parkflow_visitor_test.");
        target.SearchPath = "parkflow_rehearsal_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(target.ConnectionString);
        await connection.OpenAsync();
        async Task Execute(string sql) { await using var cmd = new NpgsqlCommand(sql, connection); await cmd.ExecuteNonQueryAsync(); }
        async Task<string[]> Rows(string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync();
            var result = new List<string>(); while (await reader.ReadAsync()) result.Add(reader.GetValue(0).ToString()!);
            return result.ToArray();
        }
        await Execute("CREATE SCHEMA \"" + target.SearchPath + "\"");
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(target.ConnectionString).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261021104419_AddDriverDocumentsAndDurableSettings");
        var historyBefore = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var user = Guid.NewGuid(); var guard = Guid.NewGuid(); var v1 = Guid.NewGuid(); var v2 = Guid.NewGuid();
        var registeredId = Guid.NewGuid(); var corId = Guid.NewGuid();
        var s1 = Guid.NewGuid(); var s2 = Guid.NewGuid(); var s3 = Guid.NewGuid();
        // Synthetic IDs/documents only; no application seeder, mail, storage or production configuration.
        await Execute($$"""
            INSERT INTO "UserAccounts" ("Id","PasswordHash","PhoneNumber","AuthProvider","Status","OnboardingStep") VALUES ('{{user}}','synthetic-hash','09170000000',0,2,0);
            INSERT INTO "UserProfiles" ("Id","UserAccountId","FirstName","LastName") VALUES ('{{guard}}','{{user}}','Synthetic','Guard');
            INSERT INTO "Guards" ("UserProfileId","AssignedGate") VALUES ('{{guard}}',1);
            INSERT INTO "CorSubmissions" ("Id","UserAccountId","AcademicTerm","CorDocumentUrl","VerificationStatus") VALUES ('{{corId}}','{{user}}','2026-2027','synthetic/cor.pdf',2);
            INSERT INTO "Vehicles" ("Id","OwnerId","PlateNumber","Brand","QrCodeHash","VehicleType","IsPrimary") VALUES ('{{registeredId}}','{{user}}','RG-1234','Synthetic','synthetic-qr',2,true);
            INSERT INTO "Visitors" ("Id","FullName","ContactNumber","PlateNumber","VehicleType","Brand","DriversLicenseUrl","ProfilePictureUrl","VehiclePictureUrl","CreatedAt") VALUES
              ('{{v1}}','Synthetic One','09170000001','AB-1234',2,'Synthetic car','synthetic/license1','synthetic/profile1','synthetic/car','2026-10-20T00:00:00Z'),
              ('{{v2}}','Synthetic Two','09170000002','MC 1234',0,'Synthetic motorcycle','synthetic/license2','synthetic/profile2','synthetic/motor','2026-10-20T00:00:00Z');
            INSERT INTO "VisitSessions" ("Id","VisitorId","EntryTime","ExitTime","Purpose","Destination","EntryGuardId","ExitGuardId","EntryGate","ExitGate","Status","CreatedAt","DriversLicenseUrl","ProfilePictureUrl","VehiclePictureUrl","LicenseVerifiedBy","LicenseVerifiedAt") VALUES
              ('{{s1}}','{{v1}}','2026-10-21T00:00:00Z','2026-10-21T02:00:00Z','Synthetic visit','Office','{{guard}}','{{guard}}','1','2',1,'2026-10-21T00:00:00Z','synthetic/old-license','synthetic/old-profile','synthetic/old-car','{{guard}}','2026-10-21T00:00:00Z'),
              ('{{s2}}','{{v1}}','2026-10-22T00:00:00Z',NULL,'Synthetic return','Office','{{guard}}',NULL,'1',NULL,0,'2026-10-22T00:00:00Z','synthetic/new-license','synthetic/new-profile','synthetic/new-car','{{guard}}','2026-10-22T00:00:00Z'),
              ('{{s3}}','{{v2}}','2026-10-21T01:00:00Z','2026-10-21T03:00:00Z','Synthetic visit','Office','{{guard}}','{{guard}}','2','1',1,'2026-10-21T01:00:00Z','synthetic/motor-license','synthetic/motor-profile','synthetic/motor-photo','{{guard}}','2026-10-21T01:00:00Z');
            INSERT INTO "Violations" ("Id","VisitSessionId","PenaltyFee","ReferenceNumber","SettlementStatus","ViolationType","CreatedAt") VALUES
              ('{{Guid.NewGuid()}}','{{s1}}',25,'SYNTHETIC-PAID',1,0,'2026-10-21T02:00:00Z'),
              ('{{Guid.NewGuid()}}','{{s3}}',50,'SYNTHETIC-UNPAID',0,0,'2026-10-21T03:00:00Z');
            INSERT INTO "Feedbacks" ("Id","UserId","Category","Rating","Description","Status","CreatedAt","InvoiceAmount","InvoiceDescription","InvoiceNumber","InvoiceStatus") VALUES
              ('{{Guid.NewGuid()}}','{{user}}','Synthetic',5,'Synthetic historical invoice',0,'2026-10-21T03:00:00Z',75,'Historical only','SYNTHETIC-INVOICE','Paid');
            """);
        var protectedTables = new[] { "CorSubmissions", "Vehicles", "Visitors", "VisitSessions", "Violations", "Feedbacks", "Guards", "UserProfiles", "UserAccounts" };
        async Task<Dictionary<string,string[]>> Snapshot()
        {
            var result = new Dictionary<string,string[]>();
            foreach (var table in protectedTables)
            {
                result[table] = await Rows($"SELECT (to_jsonb(t) - 'VisitorVehicleId' - 'LicenseVerifiedAt' - 'LicenseVerifiedBy')::text FROM \"{table}\" t ORDER BY 1");
                if (table == "CorSubmissions") result[table] = await Rows("SELECT (to_jsonb(t) - 'OrcrDocumentUrl' - 'MotorPictureUrl')::text FROM \"CorSubmissions\" t ORDER BY 1");
            }
            // Session document/guard/timestamp fields must also be protected in full.
            result["SessionSnapshots"] = await Rows("SELECT (to_jsonb(t) - 'VisitorVehicleId')::text FROM \"VisitSessions\" t ORDER BY 1");
            return result;
        }
        var columnsBefore = await Rows("SELECT table_name || '.' || column_name FROM information_schema.columns WHERE table_schema=current_schema() ORDER BY 1");
        var before = await Snapshot();
        await migrator.MigrateAsync("20261027202102_RetireFinancialContractsPreserveHistory");
        var compatibility = await Snapshot();
        foreach (var table in before.Keys) Assert.Equal(before[table], compatibility[table]);
        await migrator.MigrateAsync("20261028094839_SeparateVisitorVehicles");
        await migrator.MigrateAsync("20261028134540_EnforceVehiclePlateOwnership");
        Assert.Equal(3, await db.Set<ParkFlow.Domain.Entities.VehiclePlateOwnership>().CountAsync());
        await migrator.MigrateAsync("20261028140506_EnsureCorDocumentColumns");
        var after = await Snapshot();
        foreach (var table in before.Keys)
        {
            Assert.Equal(before[table], after[table]);
            output.WriteLine($"{table}: before={before[table].Length}, after={after[table].Length}, identical protected records PASS");
        }
        var columnsAfter = await Rows("SELECT table_name || '.' || column_name FROM information_schema.columns WHERE table_schema=current_schema() ORDER BY 1");
        Assert.All(columnsBefore, column => Assert.Contains(column, columnsAfter));
        foreach (var table in db.Model.GetRelationalModel().Tables)
            foreach (var column in table.Columns)
                Assert.Contains(table.Name + "." + column.Name, columnsAfter);
        Assert.Equal("synthetic/cor.pdf", (await db.CorSubmissions.SingleAsync(c => c.Id == corId)).CorDocumentUrl);
        Assert.Equal(2, await db.VisitorVehicles.CountAsync());
        Assert.All(await db.VisitorVehicles.ToListAsync(), vehicle => Assert.Equal(vehicle.VisitorId, vehicle.Id));
        Assert.Equal(3, await db.VisitSessions.CountAsync(s => s.VisitorVehicleId == s.VisitorId));
        Assert.Equal(1, await db.VisitSessions.CountAsync(s => s.Status == ParkFlow.Domain.Entities.VisitSessionStatus.Inside));
        var historyAfter = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(historyBefore, historyAfter[..^4]);
        Assert.Equal(new[] { "20261027202102_RetireFinancialContractsPreserveHistory", "20261028094839_SeparateVisitorVehicles", "20261028134540_EnforceVehiclePlateOwnership", "20261028140506_EnsureCorDocumentColumns" }, historyAfter[^4..]);
        async Task Reject(string sql, string state)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => Execute(sql)); Assert.Equal(state, error.SqlState);
        }
        await Reject($$"""INSERT INTO "VisitorVehicles" ("VisitorId","PlateNumber","NormalizedPlateNumber","VehicleType","Brand") VALUES ('{{v2}}','ab 1234','AB1234',2,'Duplicate')""", PostgresErrorCodes.UniqueViolation);
        await Reject($$"""INSERT INTO "VisitSessions" ("VisitorId","VisitorVehicleId","EntryTime","Status","CreatedAt") VALUES ('{{v2}}','{{v1}}',now(),1,now())""", PostgresErrorCodes.ForeignKeyViolation);
        await Reject($$"""INSERT INTO "VisitSessions" ("VisitorId","VisitorVehicleId","EntryTime","Status","CreatedAt") VALUES ('{{v1}}','{{v1}}',now(),0,now())""", PostgresErrorCodes.UniqueViolation);
        await DatabaseStartupCheck.EnsureReadyAsync(db);
        Assert.False(db.Database.HasPendingModelChanges());
        output.WriteLine($"Schema retained for evidence: {target.SearchPath}; migration history +4, visitor vehicles +2, all original columns retained.");
    }
}
