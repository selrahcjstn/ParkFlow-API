using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Violations.DTOs;
using ParkFlow.Application.Features.Violations.Commands.ProcessViolationPayment;
using ParkFlow.Application.Features.Violations.Queries.CheckViolationPayment;
using ParkFlow.Application.Features.Violations.Queries.GetViolationHistory;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.API.Controllers;

public record ProcessViolationPaymentRequest(string ReferenceNumber);

[Route("api/violations")]
[ApiController]
public class ViolationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;

    public ViolationController(IMediator mediator, IUserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// Checks whether a violation fee has been paid.
    /// </summary>
    /// <param name="referenceNumber">The violation reference number (e.g. VIO-20260527-A1B2C3D4).</param>
    [HttpGet("payment-status/{referenceNumber}")]
    [Authorize]
    public async Task<ActionResult<Result<ViolationPaymentStatusDto>>> CheckPaymentStatus(
        [FromRoute] string referenceNumber)
    {
        var result = await _mediator.Send(new CheckViolationPaymentQuery(referenceNumber));
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Returns a paginated list of violations.
    /// Guards see all violations; regular users only see their own.
    /// </summary>
    [HttpGet("history/page/{pageNumber:int}/{pageSize:int}")]
    [Authorize]
    public async Task<ActionResult<Result<PagedViolationHistoryResponse>>> GetViolationHistory(
        int pageNumber,
        int pageSize)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<PagedViolationHistoryResponse>.Failure(
                "User not identified.", ErrorCode.Unauthorized));

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 15;
        if (pageSize > 45) pageSize = 45;

        var result = await _mediator.Send(new GetViolationHistoryQuery(userId, pageNumber, pageSize));

        if (result.IsSuccess)
            return Ok(result);

        return this.ToActionResult(result);
    }

    /// <summary>
    /// Verifies and processes payment for a violation, marking it as settled.
    /// Only guards are allowed to perform this action.
    /// </summary>
    [HttpPost("process-payment")]
    [Authorize]
    public async Task<ActionResult<Result<ViolationPaymentReceiptDto>>> ProcessPayment(
        [FromBody] ProcessViolationPaymentRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<ViolationPaymentReceiptDto>.Failure(
                "User not identified.", ErrorCode.Unauthorized));

        var result = await _mediator.Send(new ProcessViolationPaymentCommand(request.ReferenceNumber, userId));
        return this.ToActionResult(result);
    }
}

