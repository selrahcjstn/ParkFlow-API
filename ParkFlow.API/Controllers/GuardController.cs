using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Queries.VerifyStudentScan;
using ParkFlow.Application.Features.RegisterGuard.Commands.CreateGuardAccount;

namespace ParkFlow.API.Controllers;

[Route("api/guards")]
[ApiController]
public class GuardController : ControllerBase
{
    private readonly IMediator _mediator;

    public GuardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new guard account with login credentials.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("create")]
    public async Task<ActionResult<Result<Guid>>> Create([FromBody] CreateGuardAccountCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Verifies a student ID QR code or student number for campus entry.
    /// </summary>
    [HttpPost("verify-student-scan")]
    public async Task<ActionResult<Result<VerifyStudentScanResponse>>> VerifyStudentScan([FromBody] VerifyStudentScanQuery query)
    {
        var result = await _mediator.Send(query);
        return this.ToActionResult(result);
    }
}