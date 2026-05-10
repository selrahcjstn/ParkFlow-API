using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Cor.CreateCorSubmission;

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

    [HttpPost]
    public async Task<ActionResult<Result<Guid>>> Create(CreateCorSubmissionCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
            return Ok(result);

        return result.ErrorCode == ErrorCode.Conflict
            ? Conflict(result)
            : BadRequest(result);
    }
}
