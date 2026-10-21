using Microsoft.AspNetCore.Authorization;
using ParkFlow.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Cor.Commands.CreateCorSubmission;
using ParkFlow.Application.Features.Cor.Commands.DeleteCorSubmission;
using ParkFlow.Application.Features.Cor.Commands.UpdateCorSubmission;
using ParkFlow.Application.Features.Cor.Commands.UpdateCorSchedules;
using ParkFlow.Application.Features.Cor.Commands.ValidateCorSubmission;
using ParkFlow.Application.Features.Cor.DTOs;
using ParkFlow.Application.Features.Cor.Queries.ListCorSubmissions;

namespace ParkFlow.API.Controllers;

[Route("api/cor-submissions")]
[ApiController]
[Authorize]
public class CorSubmissionController : ControllerBase
{
    private readonly IMediator _mediator;

    public CorSubmissionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("create")]
    public async Task<ActionResult<Result<Guid>>> Create(CreateCorSubmissionCommand command)
    {
        if (command.UserId != HttpContext.RequestServices.GetRequiredService<IUserContext>().GetUserId()) return Forbid();
        var latest = await HttpContext.RequestServices.GetRequiredService<ICorSubmissionRepository>().GetLatestByUserIdAsync(command.UserId);
        if (latest == null || latest.CorDocumentUrl != command.CorDocumentUrl) return BadRequest("Upload the document before submitting it.");
        var result = await _mediator.Send(new UpdateCorSubmissionCommand(latest.Id, command.AcademicTerm, command.CorDocumentUrl, null));
        return this.ToActionResult(result);
    }

    [HttpGet]
    [Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<ActionResult<Result<IEnumerable<CorSubmissionDto>>>> List()
    {
        var result = await _mediator.Send(new ListCorSubmissionsQuery());
        return this.ToActionResult(result);
    }

    [HttpGet("my-submission")]
    public async Task<ActionResult<Result<CorSubmissionDto?>>> GetMySubmission()
    {
        var result = await _mediator.Send(new ParkFlow.Application.Features.Cor.Queries.GetMyCorSubmission.GetMyCorSubmissionQuery());
        return this.ToActionResult(result);
    }

    [HttpPatch("{corSubmissionId:guid}")]
    public async Task<ActionResult<Result<Guid>>> Update(Guid corSubmissionId, UpdateCorSubmissionRequest request)
    {
        var repo = HttpContext.RequestServices.GetRequiredService<ICorSubmissionRepository>();
        var subject = await repo.GetCorSubmissionAsync(corSubmissionId);
        if (subject == null) return NotFound();
        if (subject.UserAccountId != HttpContext.RequestServices.GetRequiredService<IUserContext>().GetUserId()) return Forbid();
        if (request.VerificationStatus.HasValue && request.VerificationStatus.Value != ParkFlow.Domain.Enums.CorVerificationStatus.Pending)
            return Forbid();
        if (!string.IsNullOrWhiteSpace(request.CorDocumentUrl) && request.CorDocumentUrl != subject.CorDocumentUrl)
            return BadRequest("Upload the document before updating the submission.");
        var command = new UpdateCorSubmissionCommand(
            corSubmissionId,
            request.AcademicTerm,
            request.CorDocumentUrl,
            request.VerificationStatus);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [Authorize(Policy = "AdminOrSuperAdmin")]
    [HttpPatch("{corSubmissionId:guid}/validate")]
    public async Task<ActionResult<Result<Guid>>> Validate(Guid corSubmissionId, [FromBody] ValidateCorSubmissionRequest request)
    {
        var command = new ValidateCorSubmissionCommand(corSubmissionId, request.VerificationStatus, request.RejectionReason);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [Authorize(Policy = "AdminOrSuperAdmin")]
    [HttpPut("{corSubmissionId:guid}/schedules")]
    public async Task<ActionResult<Result<bool>>> UpdateSchedules(Guid corSubmissionId, [FromBody] List<UpdateScheduleItemDto> schedules)
    {
        var repo = HttpContext.RequestServices.GetRequiredService<ICorSubmissionRepository>();
        var subject = await repo.GetCorSubmissionAsync(corSubmissionId) ?? await repo.GetLatestByUserIdAsync(corSubmissionId);
        if (subject == null) return NotFound();
        var command = new UpdateCorSchedulesCommand(corSubmissionId, schedules);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [Authorize(Policy = "AdminOrSuperAdmin")]
    [HttpDelete("{corSubmissionId:guid}")]
    public async Task<ActionResult<Result<Guid>>> Delete(Guid corSubmissionId)
    {
        var result = await _mediator.Send(new DeleteCorSubmissionCommand(corSubmissionId));
        return this.ToActionResult(result);
    }
}
