using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ParkFlow.Application.Features.Visitors.DTOs;
using ParkFlow.Application.Features.Visitors.Queries.GetPlateLookup;
using ParkFlow.Application.Features.Visitors.Commands.ExitVisitorSession;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Persistence;
using ParkFlow.Persistence.Migrations;
using Test.Support;
using Test.Features.ParkingLogs;
using Test.Features.Auth;
using Xunit;

namespace Test.Features.Visitors;

public partial class VisitorEntryTests
{
    [Theory]
    [InlineData(" abc-123 ", "ABC123")]
    [InlineData("AB C 123", "ABC123")]
    public async Task ReturningPlateLookupRequiresExplicitIdentityConfirmation(string plate, string normalized)
    {
        var repo = new VisitorRepositoryFake();
        var handler = CreateHandler(repo, new FakeVehicleRepository());
        var entry = await handler.Handle(Entry(), default);
        var lookup = new GetPlateLookupHandler(new FakeVehicleRepository(), repo, new FakeUserProfileRepository(),
            new FakeStudentRepository(), new FakePersonnelRepository(), new Test.Features.ParkingLogs.FakeAdminRepository(), new FakeParkingLogRepository());
        var preview = await lookup.Handle(new(plate), default);
        Assert.True(preview.IsSuccess);
        Assert.Null(preview.Data!.FullName); Assert.Null(preview.Data.ContactNumber); Assert.Null(preview.Data.DriversLicenseUrl);
        var confirmed = await lookup.Handle(new(plate, true), default);
        Assert.Equal(entry.Data!.VisitorId, confirmed.Data!.VisitorId);
        Assert.Equal(entry.Data.VisitorVehicleId, confirmed.Data.VisitorVehicleId);
        Assert.Equal(normalized, repo.Vehicles.Single().NormalizedPlateNumber);
        Assert.True(confirmed.Data.IdentityConfirmed);
        Assert.Equal("Juan", confirmed.Data.FullName);
        Assert.Equal(entry.Data.SessionId, confirmed.Data.ActiveSessionId);
    }

    [Fact]
    public async Task ConfirmedVisitorHasTwoIndependentVehiclesAndExactExit()
    {
        var repo = new VisitorRepositoryFake();
        var handler = CreateHandler(repo, new FakeVehicleRepository());
        var first = await handler.Handle(Entry(), default);
        var second = await handler.Handle(Entry() with { PlateNumber = "MC-456", VehicleType = VehicleType.Motorcycle,
            ExistingVisitorId = first.Data!.VisitorId, IdentityConfirmed = true, FullName = "Must not replace identity" }, default);
        Assert.True(second.IsSuccess, second.Message);
        Assert.Equal(first.Data.VisitorId, second.Data!.VisitorId);
        Assert.NotEqual(first.Data.VisitorVehicleId, second.Data.VisitorVehicleId);
        Assert.Single(repo.Visitors); Assert.Equal(2, repo.Vehicles.Count);
        Assert.Equal("Juan", repo.Visitor!.FullName);
        Assert.Equal(VehicleType.Car, repo.Vehicles[0].VehicleType);
        Assert.Equal(VehicleType.Motorcycle, repo.Vehicles[1].VehicleType);
        Assert.Equal(2, repo.Sessions.Count(s => s.Status == VisitSessionStatus.Inside));
        Assert.False((await handler.Handle(Entry(), default)).IsSuccess);
        var snapshot = repo.Sessions[0].DriversLicenseUrl;
        var guardProfile = new UserProfile(GuardUserId, "Guard", "One", null, null);
        var profiles = new FakeUserProfileRepository(); profiles.Profiles.Add(guardProfile);
        var exit = new ExitVisitorSessionHandler(repo, profiles, new FakeGuardRepository { Guard = new Guard(guardProfile, 1) }, new Test.Features.ParkingLogs.FakeAdminRepository());
        var wrong = await exit.Handle(new("MC456", GuardUserId, VisitorVehicleId: first.Data.VisitorVehicleId), default);
        Assert.False(wrong.IsSuccess);
        var result = await exit.Handle(new("ABC-123", GuardUserId, VisitorVehicleId: first.Data.VisitorVehicleId, SessionId: first.Data.SessionId), default);
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(first.Data.SessionId, result.Data!.SessionId);
        Assert.NotNull(repo.Sessions[0].ExitTime); Assert.Equal(VisitSessionStatus.Completed, repo.Sessions[0].Status);
        Assert.Equal(VisitSessionStatus.Inside, repo.Sessions[1].Status); Assert.Null(repo.Sessions[1].ExitTime);
        Assert.Equal(snapshot, repo.Sessions[0].DriversLicenseUrl);
        Assert.False((await exit.Handle(new("MC456", GuardUserId, SessionId: first.Data.SessionId), default)).IsSuccess);
        Assert.False((await exit.Handle(new("MC456", null), default)).IsSuccess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownPlateNeverInfersIdentityFromNameOrPhone(bool confirmExisting)
    {
        var repo = new VisitorRepositoryFake(); var handler = CreateHandler(repo, new FakeVehicleRepository());
        var first = await handler.Handle(Entry(), default);
        var command = Entry() with { PlateNumber = "NEW123", ExistingVisitorId = confirmExisting ? first.Data!.VisitorId : null, IdentityConfirmed = confirmExisting };
        var result = await handler.Handle(command, default);
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(confirmExisting ? 1 : 2, repo.Visitors.Count);
        Assert.Equal(confirmExisting, result.Data!.VisitorId == first.Data!.VisitorId);
    }

    [Theory]
    [InlineData("confirmation")]
    [InlineData("photo")]
    [InlineData("wrong-owner")]
    public async Task AddingVehiclePreservesIdentityAndVehicleDocumentChecks(string missing)
    {
        var repo = new VisitorRepositoryFake(); var handler = CreateHandler(repo, new FakeVehicleRepository());
        var first = await handler.Handle(Entry(), default);
        var command = Entry() with { PlateNumber = "NEW123", ExistingVisitorId = first.Data!.VisitorId, IdentityConfirmed = true };
        if (missing == "confirmation") command = command with { IdentityConfirmed = false };
        if (missing == "photo") command = command with { VehiclePhoto = null };
        if (missing == "wrong-owner") command = command with { PlateNumber = "ABC123", ExistingVisitorId = Guid.NewGuid() };
        var result = await handler.Handle(command, default);
        Assert.False(result.IsSuccess); Assert.Single(repo.Visitors); Assert.Single(repo.Vehicles); Assert.Single(repo.Sessions);
    }
}

public class VisitorVehicleMigrationTests
{
    [Fact]
    public void VisitorEndpointsRetainGuardAndAdminAuthorization()
    {
        var controller = typeof(ParkFlow.API.Controllers.VisitorController);
        foreach (var method in controller.GetMethods().Where(m => m.DeclaringType == controller))
        {
            var policies = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Select(a => a.Policy).ToArray();
            Assert.Contains(method.Name == "CreateVisitorEntry" ? "GuardOnly" : "GuardOrAdmin", policies);
            Assert.Empty(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true));
        }
    }

    [Fact]
    public void AdditiveMigrationKeepsHistoryAndRefusesConflicts()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
        Assert.False(context.Database.HasPendingModelChanges());
        var migration = new SeparateVisitorVehicles();
        Assert.DoesNotContain(migration.UpOperations, o => o.IsDestructiveChange || o is DropTableOperation or DropColumnOperation or DeleteDataOperation or UpdateDataOperation);
        Assert.Contains(migration.UpOperations.OfType<CreateTableOperation>(), t => t.Name == "VisitorVehicles");
        Assert.Contains("RAISE EXCEPTION", SeparateVisitorVehicles.PreflightSql);
        Assert.Contains("HAVING count(*) > 1", SeparateVisitorVehicles.PreflightSql);
        Assert.DoesNotMatch(@"(?i)\b(UPDATE|INSERT)\b", SeparateVisitorVehicles.PreflightSql);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(SeparateVisitorVehicles.BackfillSql, @"(?i)\bUPDATE\b").Cast<System.Text.RegularExpressions.Match>());
        Assert.Contains("UPDATE \"VisitSessions\" SET \"VisitorVehicleId\" = \"VisitorId\"", SeparateVisitorVehicles.BackfillSql);
        Assert.DoesNotContain("EntryTime", SeparateVisitorVehicles.BackfillSql);
        Assert.DoesNotContain("ExitTime", SeparateVisitorVehicles.BackfillSql);
        Assert.DoesNotContain("DriversLicenseUrl", SeparateVisitorVehicles.BackfillSql);
        var id = context.Database.GetMigrations().Single(m => m.EndsWith("_SeparateVisitorVehicles", StringComparison.Ordinal));
        var sql = context.GetService<IMigrator>().GenerateScript("20261027202102_RetireFinancialContractsPreserveHistory", id);
        Assert.DoesNotMatch(@"(?i)\b(DELETE\s+FROM|DROP\s+(TABLE|COLUMN)|TRUNCATE)\b", sql);
        Assert.Contains("FOREIGN KEY (\"VisitorVehicleId\", \"VisitorId\")", sql);
        Assert.Contains("WHERE \"Status\" = 0", sql);
        var model = context.Model.FindEntityType(typeof(VisitorVehicle))!;
        Assert.True(model.GetIndexes().Single(i => i.Properties.Any(p => p.Name == "NormalizedPlateNumber")).IsUnique);
        var sessions = context.Model.FindEntityType(typeof(VisitSession))!;
        Assert.Contains(sessions.GetForeignKeys(), k => k.PrincipalEntityType.ClrType == typeof(VisitorVehicle) && k.Properties.Count == 2);
    }
}
