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
    public async Task<IActionResult> GetDocument([FromQuery] string url, [FromQuery] bool download = false, string? ticket = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest("Document URL or identifier is required.");
        }

        if (ParkFlow.API.Security.PrivateDocumentLinks.IsPrivate(url))
        {
            var links = HttpContext.RequestServices.GetRequiredService<ParkFlow.API.Security.PrivateDocumentLinks>();
            if (!links.Valid(url, ticket) && !await CanReadPrivateAsync(url)) return Unauthorized();
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers["Referrer-Policy"] = "no-referrer";
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
    public async Task<IActionResult> GetDocumentUrl([FromQuery] string url, [FromQuery] bool download = false)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest("Document URL or identifier is required.");
        }

        if (ParkFlow.API.Security.PrivateDocumentLinks.IsPrivate(url))
        {
            if (!await CanReadPrivateAsync(url)) return Unauthorized();
            var links = HttpContext.RequestServices.GetRequiredService<ParkFlow.API.Security.PrivateDocumentLinks>();
            Response.Headers.CacheControl = "no-store";
            return Ok(new { url = links.Create(url) });
        }
        var authUrl = _cloudinaryService.GetAuthenticatedDownloadUrl(url, download);
        if (string.IsNullOrWhiteSpace(authUrl))
        {
            return NotFound("Authenticated URL could not be generated.");
        }

        return Ok(new { url = authUrl });
    }

    private async Task<bool> CanReadPrivateAsync(string url)
    {
        if (User.Identity?.IsAuthenticated != true) return false;
        var authorization = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
        if ((await authorization.AuthorizeAsync(User, null, "GuardOrAdmin")).Succeeded) return true;
        var owner = HttpContext.RequestServices.GetRequiredService<IUserContext>().GetUserId();
        var profile = await HttpContext.RequestServices.GetRequiredService<IUserProfileRepository>().GetByUserIdAsync(owner);
        if (profile?.DriversLicenseUrl == url) return true;
        var submission = await HttpContext.RequestServices.GetRequiredService<ICorSubmissionRepository>().GetLatestByUserIdAsync(owner);
        return submission?.EmployeeScheduleDocumentUrl == url;
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
