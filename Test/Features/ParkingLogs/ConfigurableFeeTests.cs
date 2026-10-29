using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Persistence.Repositories;
using Test.Features.Visitors;
using Test.Support;
using Xunit;

namespace Test.Features.ParkingLogs;

public class ConfigurableFeeDefaultsTests
{
    [Theory]
    [InlineData(VehicleType.Car)]
    [InlineData(VehicleType.Motorcycle)]
    public void EveryNewRateDefaultsToZeroAndCanBeConfiguredIndependently(VehicleType type)
    {
        var settings = new SystemSettingsDto();
        var rates = settings.ForVehicle(type);
        foreach (var property in typeof(VehicleParkingSettingsDto).GetProperties().Where(p => p.PropertyType == typeof(decimal)))
            Assert.Equal(0m, property.GetValue(rates));
        settings.CarParking.EntryFee = 23m; settings.MotorcycleParking.EntryFee = 9m;
        Assert.Equal(type == VehicleType.Car ? 23m : 9m, settings.ForVehicle(type).EntryFee);
    }
}

[Collection("Parking deadline settings")]
public class ConfigurableFeeTests(VisitorPostgresDatabase database) : IClassFixture<VisitorPostgresDatabase>
{
    [VisitorPostgresFact]
    public async Task PositiveCheckoutPersistsOneImmutableChargeAndSettlementIsConcurrentSafe()
    {
        var before = SystemSettingsStore.Current;
        try
        {
            var settings = new SystemSettingsDto(); settings.CarParking.EntryFee = 17m; SystemSettingsStore.Replace(settings);
            await using var db = database.Context();
            var owner = new UserAccount("synthetic", "09170000000");
            var vehicle = new Vehicle(owner.Id,"F"+Guid.NewGuid().ToString("N")[..10],"Synthetic","hash",VehicleType.Car);
            var log = new ParkingLog(vehicle.Id,null,ParkingStatus.Parked,EntryMethod.Manual);
            db.AddRange(owner,vehicle,log); await db.SaveChangesAsync(); log.Exit();
            var charge = ParkingCharges.Checkout(log,vehicle,null,false,null,0,log.ExitTime!.Value)!;
            Assert.Equal(17m,charge.PenaltyFee);
            Assert.True(await new ParkingLogRepository(db).CompleteExitWithChargeAsync(log,[],charge));
            settings.CarParking.EntryFee = 999m; SystemSettingsStore.Replace(settings);
            await using var a = database.Context(); await using var b = database.Context();
            var first = (await new ViolationRepository(a).GetByLogIdAsync(log.Id))!;
            var second = (await new ViolationRepository(b).GetByLogIdAsync(log.Id))!;
            Assert.Equal(17m,first.PenaltyFee);
            first.MarkAsPaid(); second.MarkAsPaid();
            var results = await Task.WhenAll(new ViolationRepository(a).TrySettleAsync(first),new ViolationRepository(b).TrySettleAsync(second));
            Assert.Single(results,ok=>ok);
            await using var check=database.Context();
            var saved=await check.Violations.SingleAsync(v=>v.ParkingLogId==log.Id);
            Assert.Equal(17m,saved.PenaltyFee); Assert.Equal(SettlementStatus.Settled,saved.SettlementStatus);
            Assert.NotNull((await check.ParkingLogs.SingleAsync(p=>p.Id==log.Id)).ExitTime);
            SystemSettingsStore.Replace(new());
            Assert.Null(ParkingCharges.Checkout(log,vehicle,null,false,null,20,DateTime.UtcNow));
            Assert.Equal(17m,(await check.Violations.SingleAsync(v=>v.Id==saved.Id)).PenaltyFee);
        }
        finally { SystemSettingsStore.Replace(before); }
    }

    [VisitorPostgresFact]
    public async Task VisitorChargesBelongToExactVehicleAndZeroTotalsCreateNoObligation()
    {
        await using var db=database.Context();
        var person=new Visitor("Synthetic driver","09170000000","V"+Guid.NewGuid().ToString("N")[..8],VehicleType.Car,"Synthetic");
        var first=new VisitorVehicle(person.Id,person.PlateNumber,VehicleType.Car,"Synthetic");
        var second=new VisitorVehicle(person.Id,"M"+Guid.NewGuid().ToString("N")[..8],VehicleType.Motorcycle,"Synthetic");
        var a=new VisitSession(person.Id){VisitorVehicleId=first.Id};var b=new VisitSession(person.Id){VisitorVehicleId=second.Id};
        var repo=new VisitorRepository(db);await repo.SaveEntryAsync(person,first,a,true,true);await repo.SaveEntryAsync(person,second,b,false,true);
        a.MarkExit();var charge=Violation.CreateVisitorCharge(a,12m);Assert.True(await repo.CompleteVisitSessionWithChargeAsync(a,charge));
        Assert.Equal(VisitSessionStatus.Inside,(await db.VisitSessions.AsNoTracking().SingleAsync(s=>s.Id==b.Id)).Status);
        var counts=await new ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSessionCount.GetSessionCountHandler(
            new ParkingLogRepository(db),new CorSubmissionRepository(db),new ParkingScheduleRepository(db))
            .Handle(new(SystemSettingsStore.Current.TotalCapacity),default);
        Assert.Equal(1,counts.Data!.VisitorCount);Assert.Equal(counts.Data.ActiveSessionCount,counts.Data.RegisteredVehicleCount+counts.Data.VisitorCount);
        b.MarkExit();Assert.True(await repo.CompleteVisitSessionWithChargeAsync(b,null));
        Assert.Single(await db.Violations.Where(v=>v.VisitSessionId==a.Id || v.VisitSessionId==b.Id).ToListAsync());
        var saved=(await new ViolationRepository(db).GetByIdAsync(charge.Id))!;
        Assert.Equal(first.Id,saved.VisitSession!.VisitorVehicleId);Assert.Equal(first.PlateNumber,saved.VisitSession.VisitorVehicle.PlateNumber);
    }
}
