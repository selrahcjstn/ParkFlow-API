using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Cor.Commands.CreateCorSubmission;
using ParkFlow.Application.Features.Cor.Commands.DeleteCorSubmission;
using ParkFlow.Application.Features.Cor.Commands.UpdateCorSubmission;
using ParkFlow.Application.Features.Cor.Commands.ValidateCorSubmission;
using ParkFlow.Application.Features.Cor.DTOs;
using ParkFlow.Application.Features.Cor.Queries.ListCorSubmissions;

namespace ParkFlow.API.Controllers;

[Route("api/cor-submissions")]
[ApiController]
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
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpGet]
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
        var command = new UpdateCorSubmissionCommand(
            corSubmissionId,
            request.AcademicTerm,
            request.CorDocumentUrl,
            request.VerificationStatus);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpPatch("{corSubmissionId:guid}/validate")]
    public async Task<ActionResult<Result<Guid>>> Validate(Guid corSubmissionId, [FromBody] ValidateCorSubmissionRequest request)
    {
        var command = new ValidateCorSubmissionCommand(corSubmissionId, request.VerificationStatus);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpDelete("{corSubmissionId:guid}")]
    public async Task<ActionResult<Result<Guid>>> Delete(Guid corSubmissionId)
    {
        var result = await _mediator.Send(new DeleteCorSubmissionCommand(corSubmissionId));
        return this.ToActionResult(result);
    }
}
