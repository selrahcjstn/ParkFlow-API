using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.History.Queries;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Features.Visitors.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Persistence;
using ParkFlow.Persistence.Migrations;
using Xunit;

namespace Test.Features.ParkingLogs;

public class RetiredFinancialFeaturesTests
{
    [Fact]
    public void ModelRetainsOperationalTablesAndMatchesMigrationSnapshot()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
        var tables = db.Model.GetEntityTypes().Select(t => t.GetTableName()).ToHashSet();
        Assert.DoesNotContain("Violations", tables);
        foreach (var table in new[] { "ParkingLogs", "VisitSessions", "Visitors", "Vehicles", "UserAccounts", "ParkingReservations" })
            Assert.Contains(table, tables);
        Assert.True(db.Model.FindEntityType(typeof(VisitSession))!.FindProperty(nameof(VisitSession.Status))!.IsConcurrencyToken);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void MigrationRetainsHistoryAndRejectsAnAppliedDeletionConflict()
    {
        var migration = new RetireFinancialContractsPreserveHistory();
        var sql = Assert.IsType<SqlOperation>(Assert.Single(migration.UpOperations)).Sql;
        Assert.Contains(DatabaseStartupCheck.DeferredDeletionMigration, sql);
        Assert.Contains("RAISE EXCEPTION", sql);
        Assert.Contains("to_regclass", sql);
        Assert.DoesNotContain("DELETE FROM", sql);
        Assert.DoesNotContain("DROP ", sql);
        Assert.DoesNotContain("UPDATE ", sql);
        Assert.Empty(migration.DownOperations);
    }

    [Fact]
    public void ApiContractsAndEndpointsDoNotExposeFinancialFeatures()
    {
        foreach (var type in new[] { typeof(ExitParkingLogResponse), typeof(CreateParkingLogResponse),
                     typeof(GetActiveParkingSessionResponse), typeof(VisitorExitResponse), typeof(SystemSettingsDto),
                     typeof(ParkFlow.Application.Features.History.DTOs.ParkingHistoryResponse) })
        {
            Assert.DoesNotContain(type.GetProperties(), p =>
                new[] { "PenaltyFee", "EntryFee", "Amount", "AccruedCharge", "ChargeAmount", "SettlementStatus", "HasViolation", "IsPaid", "FeeCalculationMode" }.Contains(p.Name));
        }
        Assert.DoesNotContain(typeof(ParkFlow.API.Controllers.ParkingLogController).Assembly.GetTypes(),
            t => t.Name == "ViolationController");
        Assert.DoesNotContain(typeof(GetParkingHistoryHandler).Assembly.GetTypes(),
            t => t.Namespace?.Contains("Features.Violations") == true);
    }

    [Fact]
    public void FeedbackReplyKeepsMessageAndResolutionWithoutIssuingAnInvoice()
    {
        var feedback = new Feedback(Guid.NewGuid(), "general", 5, "A parking question");
        feedback.Reply("Answer from administrator", markResolved: true);
        Assert.Equal("Answer from administrator", feedback.AdminReplyMessage);
        Assert.NotNull(feedback.AdminRepliedAt);
        Assert.Equal(FeedbackStatus.Resolved, feedback.Status);
        Assert.DoesNotContain(typeof(Feedback).GetProperties(), property => property.Name.StartsWith("Invoice"));
        Assert.DoesNotContain(typeof(ParkFlow.Application.Features.Feedbacks.DTOs.FeedbackDto).GetProperties(), property => property.Name.StartsWith("Invoice"));
    }

    [Theory]
    [InlineData(EntryMethod.Manual, false, false)]
    [InlineData(EntryMethod.QrCode, true, false)]
    [InlineData(EntryMethod.QrCode, true, true)]
    public async Task ParkingHistoryKeepsTimesDurationAndOperationalOverstay(EntryMethod method, bool reserved, bool special)
    {
        var owner = Guid.NewGuid();
        var vehicle = new Vehicle(owner, "ABC123", "Toyota", "qr", VehicleType.Car);
        var day = new DateTime(2026, 10, 28);
        var entry = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(day, TimeSpan.FromHours(8));
        var exit = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(day.AddDays(special ? 1 : 0), TimeSpan.FromHours(10));
        var log = new ParkingLog(vehicle.Id, null, ParkingStatus.Exited, method);
        typeof(ParkingLog).GetProperty("Vehicle")!.SetValue(log, vehicle);
        typeof(ParkingLog).GetProperty("EntryTime")!.SetValue(log, entry);
        typeof(ParkingLog).GetProperty("ExitTime")!.SetValue(log, exit);
        var reservations = new FakeTestParkingReservationRepository();
        if (reserved)
        {
            var reservation = new ParkingReservation(owner, "RES-HISTORY", day, TimeSpan.FromHours(8), TimeSpan.FromHours(9), "Visit", type: special ? ReservationType.Special : ReservationType.Normal, vehicleId: vehicle.Id);
            reservation.MarkCompleted();
            await reservations.AddAsync(reservation);
        }
        var handler = new GetParkingHistoryHandler(new HistoryLogs(log), new Test.Support.FakeUserProfileRepository(),
            new Test.Support.FakeGuardRepository(), new Test.Support.FakeStudentRepository(),
            new Test.Support.FakePersonnelRepository(), new Test.Support.FakeAdminRepository(),
            new ParkingLogRoleService(), new Test.Support.FakeCorSubmissionRepository(),
            new Test.Support.FakeParkingScheduleRepository(), reservations);
        var result = await handler.Handle(new(Guid.Empty), default);
        Assert.True(result.IsSuccess, result.Message);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal(log.Id, item.SessionId);
        Assert.Equal(entry, item.EntryTime);
        Assert.Equal(exit, item.ExitTime);
        Assert.Equal(special ? 26d : 2d, item.ParkingDuration);
        Assert.Equal("Completed", item.Status);
        var grace = SystemSettingsStore.Current.IsGracePeriodEnabled ? SystemSettingsStore.Current.GracePeriodMinutes : 0;
        Assert.Equal(reserved && !special ? Math.Round(Math.Max(0, 1 - grace / 60d), 2) : 0, item.OverstayHours);
        var json = JsonSerializer.Serialize(item);
        Assert.DoesNotContain("Fee", json);
        Assert.DoesNotContain("Violation", json);
    }

    private sealed class HistoryLogs(ParkingLog log) : Test.Support.FakeParkingLogRepository, IParkingLogRepository
    {
        public new Task<IReadOnlyList<ParkingLog>> GetParkingHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15)
            => Task.FromResult<IReadOnlyList<ParkingLog>>([log]);
    }
}
