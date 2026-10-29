using ParkFlow.Application.Features.Visitors.Commands.CreateVisitorEntry;
using ParkFlow.Application.Features.Visitors.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Auth;
using Test.Support;
using Test.Features.ParkingLogs;
using Xunit;

namespace Test.Features.Visitors;

public partial class VisitorEntryTests
{
    [Theory]
    [InlineData("FullName")]
    [InlineData("ContactNumber")]
    [InlineData("PlateNumber")]
    public void VisitorIdentityFieldsAreMandatory(string field)
    {
        var request=Entry();
        request=field switch { "FullName"=>request with {FullName=""}, "ContactNumber"=>request with {ContactNumber=""}, _=>request with {PlateNumber=""} };
        var validation=new ParkFlow.Application.Features.Visitors.Commands.CreateVisitorEntry.CreateVisitorEntryValidator().Validate(request);
        Assert.Contains(validation.Errors,error=>error.PropertyName==field);
    }
    private static readonly Guid GuardUserId = Guid.Parse("3f7609df-3583-4760-8380-d9e4133b702f");
    [Theory]
    [InlineData(Roles.UniversityStaff, "Faculty")]
    [InlineData(Roles.NonAcademicPersonnel, "University Staff")]
    public async Task RegisteredPlateLookupUsesTheActualEmployeeRole(Roles role, string label)
    {
        var profile = new UserProfile(Guid.NewGuid(), "Employee", "One", null, null);
        var profiles = new FakeUserProfileRepository();
        profiles.Profiles.Add(profile);
        var personnel = new Test.Features.Users.FakePersonnelRepository();
        await personnel.AddAsync(new Personnel(profile.Id, "EMP-1", "Office", role));
        var vehicles = new FakeVehicleRepository();
        await vehicles.AddAsync(new Vehicle(profile.UserAccountId, "EMP123", "Toyota", "qr", VehicleType.Car));
        var handler = new ParkFlow.Application.Features.Visitors.Queries.GetPlateLookup.GetPlateLookupHandler(
            vehicles, new VisitorRepositoryFake(), profiles, new FakeStudentRepository(), personnel,
            new Test.Features.ParkingLogs.FakeAdminRepository(), new FakeParkingLogRepository());
        var result = await handler.Handle(new("EMP123"), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(label, result.Data!.OwnerRole);
    }

    [Fact]
    public async Task FirstVisit_CreatesVisitorAndVisit_NotRegisteredVehicle()
    {
        var visitors = new VisitorRepositoryFake();
        var vehicles = new FakeVehicleRepository();
        var handler = CreateHandler(visitors, vehicles);
        var result = await handler.Handle(Entry(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsReturning);
        Assert.Equal("Juan", visitors.Visitor!.FullName);
        Assert.Single(visitors.Sessions);
        Assert.Null(await vehicles.GetByPlateNumberAsync("ABC123"));
    }

    [Fact]
    public async Task ReturningVisit_ReusesDetailsAndCreatesAnotherSession()
    {
        var visitors = new VisitorRepositoryFake();
        var handler = CreateHandler(visitors, new FakeVehicleRepository());
        await handler.Handle(Entry(), CancellationToken.None);
        visitors.Sessions.Single().MarkExit();
        var id = visitors.Visitor!.Id;
        var result = await handler.Handle(Entry() with { FullName = "Visitor", ContactNumber = visitors.Visitor!.ContactNumber, Brand = "Changed", License = null, ProfilePhoto = null, VehiclePhoto = null }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.IsReturning);
        Assert.Equal(id, result.Data.VisitorId);
        Assert.Equal("Juan", result.Data.FullName);
        Assert.Equal("Toyota", result.Data.Brand);
        Assert.Equal("09171234567", visitors.Visitor.ContactNumber);
        Assert.Equal(2, visitors.Sessions.Count);
    }

    [Fact]
    public async Task DuplicateEntry_DoesNotOverwriteDetailsOrCreateSession()
    {
        var visitors = new VisitorRepositoryFake();
        var handler = CreateHandler(visitors, new FakeVehicleRepository());
        await handler.Handle(Entry(), CancellationToken.None);
        var result = await handler.Handle(Entry() with { FullName = "Wrong driver" }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Single(visitors.Sessions);
        Assert.Equal("Juan", visitors.Visitor!.FullName);
    }

    [Fact]
    public async Task RegisteredPlate_DoesNotCreateVisitor()
    {
        var visitors = new VisitorRepositoryFake();
        var vehicles = new FakeVehicleRepository();
        await vehicles.AddAsync(new Vehicle(Guid.NewGuid(), "ABC123", "Toyota", "qr", VehicleType.Car));
        var result = await CreateHandler(visitors, vehicles).Handle(Entry(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Null(visitors.Visitor);
        Assert.Empty(visitors.Sessions);
    }

    [Theory]
    [InlineData("phone")]
    [InlineData("license-check")]
    [InlineData("guard")]
    [InlineData("license")]
    [InlineData("profile-photo")]
    [InlineData("vehicle-photo")]
    public async Task MissingVisitorEntryPrerequisiteDoesNotPersist(string missing)
    {
        var visitors = new VisitorRepositoryFake();
        var request = missing switch
        {
            "phone" => Entry() with { ContactNumber = "" },
            "license-check" => Entry() with { LicenseVerified = false },
            "guard" => Entry() with { GuardUserId = null },
            "license" => Entry() with { License = null },
            "profile-photo" => Entry() with { ProfilePhoto = null },
            _ => Entry() with { VehiclePhoto = null }
        };
        var result = await CreateHandler(visitors, new FakeVehicleRepository()).Handle(request, default);
        Assert.False(result.IsSuccess);
        Assert.Null(visitors.Visitor);
        Assert.Empty(visitors.Sessions);
    }

    private static CreateVisitorEntryCommand Entry() =>
        new("ABC123", "Juan", "09171234567", VehicleType.Car, "Toyota", GuardUserId: GuardUserId,
            License: Image(), ProfilePhoto: Image(), VehiclePhoto: Image(), LicenseVerified: true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VisitorLookupPreservesEntryAvailability(bool returning)
    {
        var visitors = new VisitorRepositoryFake();
        if (returning)
            await visitors.AddAsync(new Visitor("Juan", null, "ABC123", VehicleType.Car, "Toyota"));
        var handler = new ParkFlow.Application.Features.Visitors.Queries.GetPlateLookup.GetPlateLookupHandler(
            new FakeVehicleRepository(), visitors, new FakeUserProfileRepository(),
            new FakeStudentRepository(), new FakePersonnelRepository(),
            new Test.Features.ParkingLogs.FakeAdminRepository(), new FakeParkingLogRepository());
        var result = await handler.Handle(new("ABC123"), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(returning ? "Visitor" : "Unknown", result.Data!.LookupType);
        Assert.Empty(visitors.Sessions);
    }

    private static Microsoft.AspNetCore.Http.IFormFile Image()
    {
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j8ZkAAAAASUVORK5CYII=");
        return new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "driver.png") { Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(), ContentType = "image/png" };
    }

    private static CreateVisitorEntryHandler CreateHandler(VisitorRepositoryFake visitors, FakeVehicleRepository vehicles)
    {
        var profiles = new FakeUserProfileRepository();
        var guardProfile = new UserProfile(GuardUserId, "Guard", "One", null, null);
        profiles.Profiles.Add(guardProfile);
        return new(visitors, new Test.Features.Files.FakeCloudinaryService(), vehicles, profiles,
            new FakeGuardRepository { Guard = new Guard(guardProfile, 1) }, new Test.Features.ParkingLogs.FakeAdminRepository());
    }

    [Fact]
    public async Task VisitorExitCompletesSessionAndRejectsRepeatedExit()
    {
        var visitors = new VisitorRepositoryFake();
        var visitor = new Visitor("Juan", "09123456789", "ABC123", VehicleType.Car, "Toyota");
        await visitors.AddAsync(visitor);
        await visitors.AddVisitSessionAsync(new VisitSession(visitor.Id));
        var guardProfile = new UserProfile(GuardUserId, "Guard", "One", null, null);
        var profiles = new FakeUserProfileRepository(); profiles.Profiles.Add(guardProfile);
        var handler = new ParkFlow.Application.Features.Visitors.Commands.ExitVisitorSession.ExitVisitorSessionHandler(
            visitors, profiles, new FakeGuardRepository { Guard = new Guard(guardProfile, 1) }, new Test.Features.ParkingLogs.FakeAdminRepository());
        var exit = await handler.Handle(new("ABC123", GuardUserId), default);
        Assert.True(exit.IsSuccess);
        Assert.Equal(VisitSessionStatus.Completed, visitors.Sessions.Single().Status);
        Assert.False((await handler.Handle(new("ABC123", GuardUserId), default)).IsSuccess);
    }



    [Fact]
    public async Task DetailHistory_IsPaged_WithoutLosingFullVisitorSummary()
    {
        var visitors = new VisitorRepositoryFake();
        var visitor = new Visitor("Juan", null, "ABC123", VehicleType.Car, "Toyota");
        await visitors.AddAsync(visitor);
        for (var i = 0; i < 27; i++)
            visitors.Sessions.Add(new VisitSession(visitor.Id) { EntryTime = DateTime.UtcNow.AddMinutes(-i) });
        var handler = new ParkFlow.Application.Features.Visitors.Queries.GetVisitorDetail.GetVisitorDetailHandler(visitors);
        var first = await handler.Handle(new(visitor.Id, 1, 10), default);
        var second = await handler.Handle(new(visitor.Id, 2, 10), default);
        var last = await handler.Handle(new(visitor.Id, 3, 10), default);
        Assert.Equal(10, first.Data!.Visits.Count());
        Assert.Equal(10, second.Data!.Visits.Count());
        Assert.Equal(7, last.Data!.Visits.Count());
        Assert.Equal(27, first.Data.Visitor.TotalVisits);
        Assert.Equal(27, last.Data.TotalCount);
        Assert.True(last.Data.Visitor.IsInside);
        Assert.Empty(first.Data.Visits.Select(v => v.Id).Intersect(second.Data.Visits.Select(v => v.Id)));
        var bounded = await handler.Handle(new(visitor.Id, 0, 1000), default);
        Assert.Equal(1, bounded.Data!.Page);
        Assert.Equal(100, bounded.Data.PageSize);
        var missing = await handler.Handle(new(Guid.NewGuid()), default);
        Assert.False(missing.IsSuccess);
    }

    private sealed class VisitorRepositoryFake : IVisitorRepository
    {
        public List<Visitor> Visitors { get; } = [];
        public List<VisitorVehicle> Vehicles { get; } = [];
        public Visitor? Visitor => Visitors.FirstOrDefault();
        public Task<VisitorVehicle?> GetVehicleByPlateNumberAsync(string plate) => Task.FromResult(Vehicles.SingleOrDefault(v => v.NormalizedPlateNumber == VisitorVehicle.NormalizePlate(plate)));
        public Task<VisitSession?> GetActiveVisitSessionByVehicleIdAsync(Guid id) => Task.FromResult(Sessions.SingleOrDefault(s => s.VisitorVehicleId == id && s.Status == VisitSessionStatus.Inside));
        public Task SaveEntryAsync(Visitor visitor, VisitorVehicle vehicle, VisitSession session, bool newVisitor, bool newVehicle, CancellationToken ct = default)
        {
            if (newVisitor) Visitors.Add(visitor);
            if (newVehicle) { Vehicles.Add(vehicle); visitor.Vehicles.Add(vehicle); }
            session.Visitor = visitor; session.VisitorVehicle = vehicle;
            Sessions.Add(session); return Task.CompletedTask;
        }
        public List<VisitSession> Sessions { get; } = [];
        public Task<Visitor?> GetByIdAsync(Guid id) => Task.FromResult(Visitors.FirstOrDefault(v => v.Id == id));
        public Task<Visitor?> GetByPlateNumberAsync(string plate) => Task.FromResult(Visitor?.PlateNumber == plate ? Visitor : null);
        public Task<VisitSession?> GetActiveVisitSessionByVisitorIdAsync(Guid id) =>
            Task.FromResult(Sessions.FirstOrDefault(s => s.VisitorId == id && s.Status == VisitSessionStatus.Inside));
        public Task AddAsync(Visitor visitor) { Visitors.Add(visitor); var vehicle = new VisitorVehicle(visitor.Id, visitor.PlateNumber, visitor.VehicleType, visitor.Brand) { Visitor = visitor, VehiclePictureUrl = visitor.VehiclePictureUrl }; Vehicles.Add(vehicle); visitor.Vehicles.Add(vehicle); return Task.CompletedTask; }
        public Task AddVisitSessionAsync(VisitSession session) { session.Visitor = Visitors.Single(v => v.Id == session.VisitorId); session.VisitorVehicle = Vehicles.First(v => v.VisitorId == session.VisitorId); session.VisitorVehicleId = session.VisitorVehicle.Id; Sessions.Add(session); return Task.CompletedTask; }
        public Task UpdateAsync(Visitor visitor) => Task.CompletedTask;
        public Task UpdateVisitSessionAsync(VisitSession session) => Task.CompletedTask;
        public Task<bool> CompleteVisitSessionAsync(VisitSession session) { return Task.FromResult(true); }
        public Task DeleteAsync(Visitor visitor) => throw new NotImplementedException();
        public Task<VisitorDetailDto?> GetDetailPageAsync(Guid id, int page, int size, Guid? visitorVehicleId = null)
        {
            if (Visitor?.Id != id) return Task.FromResult<VisitorDetailDto?>(null);
            var all = Sessions.Where(s => s.VisitorId == id && (!visitorVehicleId.HasValue || s.VisitorVehicleId == visitorVehicleId)).OrderByDescending(s => s.EntryTime).ThenByDescending(s => s.Id).ToList();
            var visitor = new VisitorDto(Visitor.Id, Visitor.FullName, Visitor.PlateNumber, Visitor.Brand,
                (int)Visitor.VehicleType, Visitor.ContactNumber, all.FirstOrDefault()?.EntryTime, all.Count,
                all.Any(s => s.Status == VisitSessionStatus.Inside));
            var visits = all.Skip((page - 1) * size).Take(size).Select(s => new VisitSessionDto(s.Id,
                s.EntryTime, s.ExitTime, s.Purpose, s.Destination, null, null, s.EntryGate, s.ExitGate, s.Status.ToString()));
            return Task.FromResult<VisitorDetailDto?>(new VisitorDetailDto(visitor, visits, page, size, all.Count));
        }
        public Task<Visitor?> GetWithSessionsByIdAsync(Guid id) => throw new NotImplementedException();
        public Task<Visitor?> GetWithSessionsByPlateNumberAsync(string plate) => throw new NotImplementedException();
        public Task<VisitSession?> GetActiveVisitSessionByPlateNumberAsync(string plate) => throw new NotImplementedException();
        public Task<VisitSession?> GetVisitSessionByIdAsync(Guid id) => throw new NotImplementedException();
        public Task<int> GetActiveVisitSessionCountAsync() => throw new NotImplementedException();
        public Task<(IEnumerable<VisitorDto> Items, int TotalCount)> GetPagedVisitorsAsync(int page, int size, string? search, bool? onlyInside = null) =>
            throw new NotImplementedException();
    }
}
