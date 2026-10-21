using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.API.Controllers;

[ApiController, Route("api/verification"), Authorize]
public class VerificationController(IMediator mediator, IUserContext user) : ControllerBase
{
    [HttpGet("policies"), Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<IActionResult> Policies(CancellationToken ct) => Ok(await mediator.Send(new GetVerificationPolicy(), ct));

    [HttpPut("policies"), Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult<Result<bool>>> UpdatePolicies(VerificationPolicy request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateVerificationPolicy(request, user.GetUserId()), ct);
        return this.ToActionResult(result);
    }

    [HttpGet("my")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await mediator.Send(new ListVerifications(user.GetUserId(), false), ct));
    }

    [HttpGet("cor-schedule")]
    public async Task<IActionResult> CorSchedule(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await mediator.Send(new GetCorSchedulePreview(user.GetUserId()), ct));
    }

    public record FetchInformationRequest(Guid RecordId, int Version);
    [HttpPost("cor-information/fetch")]
    public async Task<ActionResult<Result<CorSchedulePreview>>> FetchInformation(FetchInformationRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new FetchCorInformation(user.GetUserId(), request.RecordId, request.Version), ct));

    public record ConfirmScheduleRequest(Guid RecordId, int Version, List<CorWeekDay>? Week = null);
    [HttpPost("cor-schedule/confirm")]
    public async Task<ActionResult<Result<bool>>> ConfirmSchedule(ConfirmScheduleRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new ConfirmCorSchedule(request.RecordId, request.Version, user.GetUserId(), request.Week), ct));

    public record ScheduleReviewRequest(Guid RecordId, int Version, string Reason);
    [HttpPost("cor-schedule/request-review")]
    public async Task<ActionResult<Result<bool>>> RequestScheduleReview(ScheduleReviewRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new RequestCorScheduleReview(user.GetUserId(), request.RecordId, request.Version, request.Reason), ct));

    public record ScheduleCorrectionRequest(int Version, List<CorWeekDay> Week, string Reason, bool CorReviewed);
    [HttpPost("records/{id:guid}/cor-schedule"), Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<ActionResult<Result<bool>>> CorrectSchedule(Guid id, ScheduleCorrectionRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new CorrectCorSchedule(id, request.Version, user.GetUserId(), request.Week, request.Reason, request.CorReviewed), ct));

    [HttpGet("records"), Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<IActionResult> Records([FromQuery] bool history, [FromQuery] Guid? userId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await mediator.Send(new ListVerifications(userId, history, true), ct));
    }

    [HttpGet("audit"), Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<IActionResult> Audit([FromQuery] Guid? recordId, CancellationToken ct) =>
        Ok(await mediator.Send(new ListVerificationAudit(recordId), ct));

    public record ReviewRequest(int Version, string Status, string Reason);
    [HttpPost("records/{id:guid}/review"), Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<ActionResult<Result<bool>>> Review(Guid id, ReviewRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new ReviewVerification(id, request.Version, request.Status, request.Reason, user.GetUserId()), ct));

    public record ReviewSubjectRequest(string Status, string Reason);
    [HttpPost("{type}/{subjectId:guid}/review"), Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<ActionResult<Result<bool>>> ReviewSubject(string type, Guid subjectId, ReviewSubjectRequest request, CancellationToken ct) =>
        this.ToActionResult(await mediator.Send(new ReviewVerificationSubject(type, subjectId, request.Status, request.Reason, user.GetUserId()), ct));

    [HttpGet("academic-term")]
    public async Task<IActionResult> AcademicTerm(CancellationToken ct)
    {
        var policy = await mediator.Send(new GetVerificationPolicy(), ct);
        return Ok(new { policy.AcademicYear, policy.Semester });
    }
}
