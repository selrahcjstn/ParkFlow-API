using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Auth.Registration;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingVehicle;
using ParkFlow.Application.Features.Vehicles.Command;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Persistence.Repositories;
using Test.Features.Visitors;
using Test.Support;
using Xunit;

namespace Test.Features.Vehicles;

[Collection("Parking deadline settings")]
public class VehiclePlateConflictTests(VisitorPostgresDatabase database) : IClassFixture<VisitorPostgresDatabase>
{
    private static (Visitor Person, VisitorVehicle Vehicle, VisitSession Session) VisitorEntry(string plate)
    {
        var person = new Visitor("Synthetic driver", "09170000000", plate, VehicleType.Car, "Synthetic");
        var vehicle = new VisitorVehicle(person.Id, plate, VehicleType.Car, "Synthetic");
        return (person, vehicle, new VisitSession(person.Id) { VisitorVehicleId = vehicle.Id });
    }
    private async Task<UserAccount> Owner()
    {
        await using var db = database.Context(); var user = new UserAccount("synthetic-hash", "09170000000");
        db.UserAccounts.Add(user); await db.SaveChangesAsync(); return user;
    }
    [VisitorPostgresFact]
    public async Task VisitorPlateCannotBeRegisteredAndExistingHistoryIsPreserved()
    {
        var entry = VisitorEntry("T" + Guid.NewGuid().ToString("N")[..10]); var owner = await Owner();
        await using var db = database.Context();
        await new VisitorRepository(db).SaveEntryAsync(entry.Person, entry.Vehicle, entry.Session, true, true);
        var vehicle = new Vehicle(owner.Id, entry.Vehicle.PlateNumber.ToLowerInvariant().Insert(3,"-"), "Synthetic", "hash", VehicleType.Car);
        var error = await Assert.ThrowsAsync<VehiclePlateConflictException>(() => new VehicleRepository(db).AddAsync(vehicle));
        Assert.Contains("review ownership", error.Message);
        await using var check = database.Context(); Assert.False(await check.Vehicles.AnyAsync(v => v.Id == vehicle.Id));
        Assert.True(await check.VisitSessions.AnyAsync(s => s.Id == entry.Session.Id && s.Status == VisitSessionStatus.Inside));
    }
    [VisitorPostgresFact]
    public async Task RegisteredPlateCannotCreateAnOrphanVisitor()
    {
        var owner = await Owner(); var plate = "T" + Guid.NewGuid().ToString("N")[..10];
        await using var db = database.Context();
        var registered = new Vehicle(owner.Id, " \t" + plate + "\r ", "Synthetic", "hash", VehicleType.Car);
        await new VehicleRepository(db).AddAsync(registered);
        Assert.Equal(plate, (await db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == registered.Id)).PlateNumber);
        var entry = VisitorEntry(plate.ToLowerInvariant().Insert(3," "));
        await Assert.ThrowsAsync<ParkingCapacityException>(() => new VisitorRepository(db).SaveEntryAsync(entry.Person, entry.Vehicle, entry.Session, true, true));
        await using var check = database.Context(); Assert.False(await check.Visitors.AnyAsync(v => v.Id == entry.Person.Id));
    }
    [VisitorPostgresFact]
    public async Task ConcurrentRegisteredAndVisitorCreationHasOnePlateOwnerAndNoOrphanVisitor()
    {
        var owner = await Owner(); var entry = VisitorEntry("T" + Guid.NewGuid().ToString("N")[..10]);
        var registered = new Vehicle(owner.Id, entry.Vehicle.PlateNumber.ToLowerInvariant().Insert(3,"-"), "Synthetic", "hash", VehicleType.Car);
        await using var a = database.Context(); await using var b = database.Context();
        async Task<bool> Register() { try { await new VehicleRepository(a).AddAsync(registered); return true; } catch (VehiclePlateConflictException) { return false; } }
        async Task<bool> Visit() { try { await new VisitorRepository(b).SaveEntryAsync(entry.Person, entry.Vehicle, entry.Session, true, true); return true; } catch (ParkingCapacityException) { return false; } }
        var result = await Task.WhenAll(Register(), Visit()); Assert.Single(result, ok => ok);
        await using var check = database.Context();
        Assert.Equal(result[1], await check.Visitors.AnyAsync(v => v.Id == entry.Person.Id));
        Assert.Equal(result[1], await check.VisitorVehicles.AnyAsync(v => v.Id == entry.Vehicle.Id));
        Assert.Equal(result[0], await check.Vehicles.AnyAsync(v => v.Id == registered.Id));
    }
    [VisitorPostgresFact]
    public async Task EditToVisitorPlateReturnsConflictWithoutChangingStoredVehicle()
    {
        var owner = await Owner(); var entry = VisitorEntry("T" + Guid.NewGuid().ToString("N")[..10]);
        var registered = new Vehicle(owner.Id, "R" + Guid.NewGuid().ToString("N")[..10], "Synthetic", "hash", VehicleType.Car);
        await using var db = database.Context(); var repo = new VehicleRepository(db); await repo.AddAsync(registered);
        await new VisitorRepository(db).SaveEntryAsync(entry.Person, entry.Vehicle, entry.Session, true, true);
        var original = registered.PlateNumber;
        var result = await new UpdateVehicleHandler(repo).Handle(new(registered.Id,owner.Id,entry.Vehicle.PlateNumber,"Synthetic",VehicleType.Car), default);
        Assert.False(result.IsSuccess); Assert.Equal(ErrorCode.Conflict,result.ErrorCode);
        await using var check = database.Context(); Assert.Equal(original,(await check.Vehicles.SingleAsync(v => v.Id == registered.Id)).PlateNumber);
    }
    [VisitorPostgresFact]
    public async Task HybridRegistrationRejectsVisitorPlateBeforeSavingAccount()
    {
        var entry = VisitorEntry("T" + Guid.NewGuid().ToString("N")[..10]);
        await using var db = database.Context(); await new VisitorRepository(db).SaveEntryAsync(entry.Person,entry.Vehicle,entry.Session,true,true);
        var id = Guid.NewGuid(); var details = new RegistrationDetails { Role=Roles.UniversityStaff,Email=id+"@example.invalid",PhoneNumber="09170000000",FirstName="Synthetic",LastName="Faculty",Department="Synthetic",IdCardNumber=id.ToString(),PlateNumber=entry.Vehicle.PlateNumber.ToLowerInvariant().Insert(3,"-"),Brand="Synthetic",VehicleType=VehicleType.Car };
        var result = await new RegistrationWriter(db).SaveAsync(new(id,details,details.Email,"synthetic/id","synthetic/orcr","synthetic/car",null,"synthetic/license","synthetic/profile"),"synthetic-hash","synthetic-qr",default);
        Assert.False(result.IsSuccess); Assert.Equal(ErrorCode.Conflict,result.ErrorCode);
        await using var check = database.Context(); Assert.False(await check.UserAccounts.AnyAsync(u => u.Id == id));
    }
    [VisitorPostgresFact]
    public async Task CreateHandlerReturnsAnActionableConflict()
    {
        var owner = await Owner(); var entry = VisitorEntry("T" + Guid.NewGuid().ToString("N")[..10]);
        await using var db = database.Context(); await new VisitorRepository(db).SaveEntryAsync(entry.Person,entry.Vehicle,entry.Session,true,true);
        var result = await new CreateVehicleHandler(new VehicleRepository(db),new CreateVehicleValidator(),new FakeQrCodeService())
            .Handle(new(owner.Id,entry.Vehicle.PlateNumber,"Synthetic",VehicleType.Car),default);
        Assert.False(result.IsSuccess); Assert.Equal(ErrorCode.Conflict,result.ErrorCode); Assert.Contains("review ownership",result.Message);
    }
    [VisitorPostgresFact]
    public async Task LegacyOnboardingCreateAndEditReturnConflictWithoutAdvancingRegistration()
    {
        var owner = await Owner(); var entry = VisitorEntry("T" + Guid.NewGuid().ToString("N")[..10]);
        await using var db = database.Context(); await new VisitorRepository(db).SaveEntryAsync(entry.Person,entry.Vehicle,entry.Session,true,true);
        var vehicles = new VehicleRepository(db);
        var handler = new UpdateOnboardingVehicleHandler(vehicles,new UserAccountRepository(db),new FakeQrCodeService(),new UpdateOnboardingVehicleValidator());
        var request = new UpdateOnboardingVehicleCommand(owner.Id,entry.Vehicle.PlateNumber,"Synthetic",VehicleType.Car);
        var create = await handler.Handle(request,default);
        Assert.False(create.IsSuccess); Assert.Equal(ErrorCode.Conflict,create.ErrorCode);
        var existing = new Vehicle(owner.Id,"R"+Guid.NewGuid().ToString("N")[..10],"Synthetic","hash",VehicleType.Car);
        var original = existing.PlateNumber; await vehicles.AddAsync(existing);
        var edit = await handler.Handle(request,default);
        Assert.False(edit.IsSuccess); Assert.Equal(ErrorCode.Conflict,edit.ErrorCode);
        await using var check = database.Context();
        Assert.Equal(original,(await check.Vehicles.SingleAsync(v => v.Id == existing.Id)).PlateNumber);
        Assert.Equal(owner.OnboardingStep,(await check.UserAccounts.SingleAsync(u => u.Id == owner.Id)).OnboardingStep);
    }

}
