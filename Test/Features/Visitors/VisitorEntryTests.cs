using ParkFlow.Application.Features.Visitors.Commands.CreateVisitorEntry;
using ParkFlow.Application.Features.Visitors.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Auth;
using Test.Features.Violations;
using Test.Features.ParkingLogs;
using Xunit;

namespace Test.Features.Visitors;

public class VisitorEntryTests
{
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
        Assert.Equal(20m, result.Data.EntryFee);
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
        var result = await handler.Handle(Entry() with { FullName = "Visitor", ContactNumber = "", Brand = "Changed" }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.IsReturning);
        Assert.Equal(20m, result.Data.EntryFee);
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

    private static CreateVisitorEntryCommand Entry() =>
        new("ABC123", "Juan", "09171234567", VehicleType.Car, "Toyota");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VisitorLookup_QuotesFeeBeforeEntry(bool returning)
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
        Assert.Equal(20m, result.Data.EntryFee);
        Assert.Empty(visitors.Sessions);
    }

    private static CreateVisitorEntryHandler CreateHandler(VisitorRepositoryFake visitors, FakeVehicleRepository vehicles) =>
        new(visitors, vehicles, new FakeUserProfileRepository(), new FakeGuardRepository(), new Test.Features.ParkingLogs.FakeAdminRepository());

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
        public Visitor? Visitor { get; private set; }
        public List<VisitSession> Sessions { get; } = [];
        public Task<Visitor?> GetByIdAsync(Guid id) => Task.FromResult(Visitor?.Id == id ? Visitor : null);
        public Task<Visitor?> GetByPlateNumberAsync(string plate) => Task.FromResult(Visitor?.PlateNumber == plate ? Visitor : null);
        public Task<VisitSession?> GetActiveVisitSessionByVisitorIdAsync(Guid id) =>
            Task.FromResult(Sessions.FirstOrDefault(s => s.VisitorId == id && s.Status == VisitSessionStatus.Inside));
        public Task AddAsync(Visitor visitor) { Visitor = visitor; return Task.CompletedTask; }
        public Task AddVisitSessionAsync(VisitSession session) { Sessions.Add(session); return Task.CompletedTask; }
        public Task UpdateAsync(Visitor visitor) => Task.CompletedTask;
        public Task UpdateVisitSessionAsync(VisitSession session) => Task.CompletedTask;
        public Task DeleteAsync(Visitor visitor) => throw new NotImplementedException();
        public Task<VisitorDetailDto?> GetDetailPageAsync(Guid id, int page, int size)
        {
            if (Visitor?.Id != id) return Task.FromResult<VisitorDetailDto?>(null);
            var all = Sessions.Where(s => s.VisitorId == id).OrderByDescending(s => s.EntryTime).ThenByDescending(s => s.Id).ToList();
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
