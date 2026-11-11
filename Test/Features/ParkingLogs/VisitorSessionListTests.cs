using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSession;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Auth;
using Test.Support;

namespace Test.Features.ParkingLogs;

public class VisitorSessionListTests
{
    // Only GetActiveVisitSessionsAsync is needed; every other member is unused by the sessions list.
    public class VisitorRepoProxy : DispatchProxy
    {
        public IReadOnlyList<VisitSession> Sessions { get; set; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method?.Name == nameof(IVisitorRepository.GetActiveVisitSessionsAsync)
                ? Task.FromResult(Sessions)
                : throw new NotSupportedException(method?.Name);
    }

    [Fact]
    public async Task ActiveSessions_IncludesVisitorsCurrentlyInside()
    {
        var visitor = new Visitor("Maria Clara Reyes", "+639171112222", "VIS-777", VehicleType.Car, "Honda");
        var vehicle = new VisitorVehicle(visitor.Id, "VIS-777", VehicleType.Car, "Honda");
        var visit = new VisitSession(visitor.Id, "Meeting", "Registrar") { Visitor = visitor, VisitorVehicle = vehicle };

        var proxy = DispatchProxy.Create<IVisitorRepository, VisitorRepoProxy>();
        ((VisitorRepoProxy)(object)proxy).Sessions = [visit];

        var handler = new GetActiveParkingSessionHandler(new FakeParkingLogRepositoryWithActiveLogs(new List<ParkingLog>()),
            new FakeParkingScheduleRepositoryWithMock([]), new FakeCorSubmissionRepositoryWithMock([]),
            new FakeAdminRepository(), new ParkingLogRoleService(), visitorRepository: proxy);

        var result = await handler.Handle(new GetActiveParkingSessionQuery(100), CancellationToken.None);

        var row = Assert.Single(result.Data!);
        Assert.Equal("Visitor", row.Role);
        Assert.Equal("Maria", row.FirstName);
        Assert.Equal("Clara Reyes", row.LastName);
        Assert.Equal("VIS-777", row.PlateNumber);
        Assert.Equal("Inside", row.Status);
        Assert.Equal("Visitor", row.EntryMethod);
    }
}
