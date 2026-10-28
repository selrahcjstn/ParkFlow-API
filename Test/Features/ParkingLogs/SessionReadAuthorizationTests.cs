using System.Reflection;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ParkFlow.API.Controllers;
using ParkFlow.API.Security;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Xunit;

namespace Test.Features.ParkingLogs;

public class SessionReadAuthorizationTests
{
    [Theory]
    [InlineData("Owner", true)]
    [InlineData("OtherStudent", false)]
    [InlineData("Guard", true)]
    [InlineData("Admin", true)]
    [InlineData("PasswordlessGuard", false)]
    [InlineData("Anonymous", false)]
    [InlineData("MissingVehicle", false)]
    public async Task IndividualSessionRequiresAnOwnerOrAuthorizedGuard(string actor, bool allowed)
    {
        var actorId = Guid.NewGuid();
        var vehicle = new Vehicle(actor == "Owner" ? actorId : Guid.NewGuid(), "ABC123", "Toyota", "hash", VehicleType.Car);
        var vehicles = new Test.Support.FakeVehicleRepository();
        if (actor != "MissingVehicle") await vehicles.AddAsync(vehicle);
        var claims = new List<Claim> { new("role", actor is "Guard" or "PasswordlessGuard" ? "Guard" : actor == "Admin" ? "Admin" : "Student") };
        if (actor is "Guard" or "Admin") claims.Add(new("parkflow_authentication_method", "pwd"));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, actor == "Anonymous" ? null : "test"));
        using var services = new ServiceCollection().AddLogging().AddAuthorization(AuthorizationPolicies.Configure).BuildServiceProvider();
        var mediator = DispatchProxy.Create<IMediator, Capture>();
        var capture = (Capture)(object)mediator;
        var controller = new ParkingLogController(mediator, new Actor(actorId), vehicles, services.GetRequiredService<IAuthorizationService>())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } } };
        var result = await controller.GetActiveSessionByVehicleId(vehicle.Id);
        Assert.Equal(allowed ? 1 : 0, capture.Calls);
        if (actor == "Anonymous") Assert.IsType<UnauthorizedResult>(result.Result);
        else if (!allowed) Assert.IsType<NotFoundResult>(result.Result);
        else Assert.IsType<OkObjectResult>(result.Result);
    }
    [Theory]
    [InlineData(nameof(ParkingLogController.GetActiveSessions))]
    [InlineData(nameof(ParkingLogController.GetParkingHistory))]
    public async Task FleetAndGlobalHistoryRejectAnonymousAndStudentPolicies(string method)
    {
        var attribute = Assert.Single(typeof(ParkingLogController).GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("GuardOrAdmin", attribute.Policy);
        using var services = new ServiceCollection().AddLogging().AddAuthorization(AuthorizationPolicies.Configure).BuildServiceProvider();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        foreach (var role in new[] { "Anonymous", "Student", "Guard", "SuperAdmin" })
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("role", role), new Claim("parkflow_authentication_method", "pwd") }, role == "Anonymous" ? null : "test"));
            var result = await authorization.AuthorizeAsync(principal, null, attribute.Policy!);
            Assert.Equal(role is "Guard" or "SuperAdmin", result.Succeeded);
        }
    }
    private sealed record Actor(Guid Id) : IUserContext { public Guid GetUserId() => Id; }
    public class Capture : DispatchProxy
    {
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Assert.Equal("Send", method?.Name); Calls++;
            return Task.FromResult(Result<ActiveParkingSessionResponse>.Success(new ActiveParkingSessionResponse { SessionId = "session", ExitBy = "2026-10-28T02:15:00Z" }));
        }
    }
}
