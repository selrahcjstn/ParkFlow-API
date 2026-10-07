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
        var result = await handler.Handle(Entry() with { FullName = "Visitor", ContactNumber = "", Brand = "Changed" }, CancellationToken.None);
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

    private static CreateVisitorEntryCommand Entry() =>
        new("ABC123", "Juan", "09171234567", VehicleType.Car, "Toyota");

    private static CreateVisitorEntryHandler CreateHandler(VisitorRepositoryFake visitors, FakeVehicleRepository vehicles) =>
        new(visitors, vehicles, new FakeUserProfileRepository(), new FakeGuardRepository(), new Test.Features.ParkingLogs.FakeAdminRepository());

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
        public Task<Visitor?> GetWithSessionsByIdAsync(Guid id) => throw new NotImplementedException();
        public Task<Visitor?> GetWithSessionsByPlateNumberAsync(string plate) => throw new NotImplementedException();
        public Task<VisitSession?> GetActiveVisitSessionByPlateNumberAsync(string plate) => throw new NotImplementedException();
        public Task<VisitSession?> GetVisitSessionByIdAsync(Guid id) => throw new NotImplementedException();
        public Task<int> GetActiveVisitSessionCountAsync() => throw new NotImplementedException();
        public Task<(IEnumerable<Visitor> Items, int TotalCount)> GetPagedVisitorsAsync(int page, int size, string? search, bool? onlyInside = null) =>
            throw new NotImplementedException();
    }
}
