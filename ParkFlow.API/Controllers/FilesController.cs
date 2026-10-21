using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.Commands.UploadProfilePicture;
using ParkFlow.Application.Features.Files.Commands.UploadCorDocument;
using ParkFlow.Application.Features.Files.Commands.UploadOrcrDocument;
using ParkFlow.Application.Features.Files.Commands.UploadMotorPicture;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Interfaces;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ParkFlow.API.Controllers;

[Route("api/files")]
[ApiController]
public class FilesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly ILogger<FilesController>? _logger;

    public FilesController(IMediator mediator, ICloudinaryService cloudinaryService, ILogger<FilesController>? logger = null)
    {
        _mediator = mediator;
        _cloudinaryService = cloudinaryService;
        _logger = logger;
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
    [Authorize]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Result<UploadFileResponse>>> UploadCorDocument([FromForm] UploadCorDocumentCommand command, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        try { return this.ToActionResult(await _mediator.Send(command, ct)); }
        finally { _logger?.LogInformation("COR upload API in {DurationMs} ms", watch.Elapsed.TotalMilliseconds); }
    }

    [HttpPost("upload/orcr")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Result<UploadFileResponse>>> UploadOrcrDocument([FromForm] UploadOrcrDocumentCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpPost("upload/motor-picture")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Result<UploadFileResponse>>> UploadMotorPicture([FromForm] UploadMotorPictureCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }
}
