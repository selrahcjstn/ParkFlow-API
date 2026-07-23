using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.Commands.UploadProfilePicture;
using ParkFlow.Application.Features.Files.Commands.UploadCorDocument;
using ParkFlow.Application.Features.Files.Commands.UploadOrcrDocument;
using ParkFlow.Application.Features.Files.Commands.UploadMotorPicture;
using ParkFlow.Application.Features.Files.DTOs;
using System.Threading.Tasks;

namespace ParkFlow.API.Controllers;

[Route("api/files")]
[ApiController]
public class FilesController : ControllerBase
{
    private readonly IMediator _mediator;

    public FilesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("upload/profile-picture")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Result<UploadFileResponse>>> UploadProfilePicture([FromForm] UploadProfilePictureCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpPost("upload/cor")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Result<UploadFileResponse>>> UploadCorDocument([FromForm] UploadCorDocumentCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpPost("upload/orcr")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Result<UploadFileResponse>>> UploadOrcrDocument([FromForm] UploadOrcrDocumentCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpPost("upload/motor-picture")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Result<UploadFileResponse>>> UploadMotorPicture([FromForm] UploadMotorPictureCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }
}
