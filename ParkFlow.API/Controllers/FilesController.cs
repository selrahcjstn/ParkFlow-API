using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.Commands.UploadProfilePicture;
using ParkFlow.Application.Features.Files.Commands.UploadCorDocument;
using ParkFlow.Application.Features.Files.Commands.UploadOrcrDocument;
using ParkFlow.Application.Features.Files.Commands.UploadMotorPicture;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Interfaces;
using System.Threading.Tasks;

namespace ParkFlow.API.Controllers;

[Route("api/files")]
[ApiController]
public class FilesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICloudinaryService _cloudinaryService;

    public FilesController(IMediator mediator, ICloudinaryService cloudinaryService)
    {
        _mediator = mediator;
        _cloudinaryService = cloudinaryService;
    }

    [HttpGet("document")]
    [HttpHead("document")]
    public async Task<IActionResult> GetDocument([FromQuery] string url, [FromQuery] bool download = false)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest("Document URL or identifier is required.");
        }

        var result = await _cloudinaryService.GetDocumentStreamAsync(url, download);
        if (result == null)
        {
            return NotFound("Document could not be retrieved.");
        }

        var (stream, contentType, fileName) = result.Value;

        if (download)
        {
            return File(stream, contentType, fileName, enableRangeProcessing: true);
        }

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
        return File(stream, contentType, enableRangeProcessing: true);
    }

    [HttpGet("document/url")]
    public IActionResult GetDocumentUrl([FromQuery] string url, [FromQuery] bool download = false)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest("Document URL or identifier is required.");
        }

        var authUrl = _cloudinaryService.GetAuthenticatedDownloadUrl(url, download);
        if (string.IsNullOrWhiteSpace(authUrl))
        {
            return NotFound("Authenticated URL could not be generated.");
        }

        return Ok(new { url = authUrl });
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
