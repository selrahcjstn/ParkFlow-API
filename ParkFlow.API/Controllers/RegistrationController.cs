using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Auth.Registration;
using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.API.Controllers;

[ApiController, AllowAnonymous, Route("api/registration")]
public class RegistrationController(IRegistrationDraftService drafts) : ControllerBase
{
    private string Token => Request.Headers["X-Registration-Token"].ToString();
    public record StartRequest(string Email, string EmailVerificationToken);
    public record FetchRequest(Guid RecordId, int Version);
    [HttpPost("session")]
    public Task<ActionResult<Result<RegistrationSession>>> Start(StartRequest request, CancellationToken ct) =>
        Run(async () => Result<RegistrationSession>.Success(await drafts.StartAsync(request.Email, request.EmailVerificationToken, ct)));
    [HttpPost("cor"), RequestSizeLimit(6 * 1024 * 1024)]
    public Task<ActionResult<Result<CorSchedulePreview>>> Upload(IFormFile file, CancellationToken ct) =>
        Run(async () => Result<CorSchedulePreview>.Success(await drafts.UploadCorAsync(Token, file, ct)));
    [HttpGet("cor")]
    public Task<ActionResult<Result<CorSchedulePreview?>>> Preview(CancellationToken ct) =>
        Run(async () => Result<CorSchedulePreview?>.Success(await drafts.PreviewAsync(Token, ct)));
    [HttpPost("cor/fetch")]
    public Task<ActionResult<Result<CorSchedulePreview>>> Fetch(FetchRequest request, CancellationToken ct) =>
        Run(async () => Result<CorSchedulePreview>.Success(await drafts.FetchAsync(Token, request.RecordId, request.Version, ct)));
    [HttpPost("complete"), RequestSizeLimit(17 * 1024 * 1024)]
    public Task<ActionResult<Result<string>>> Complete([FromForm] string details, IFormFile? cor, IFormFile orcr, IFormFile motor, CancellationToken ct) =>
        Run(async () =>
        {
            RegistrationDetails? data;
            try { data = JsonSerializer.Deserialize<RegistrationDetails>(details, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException) { return Result<string>.Failure("Check the registration details and try again.", ErrorCode.BadRequest); }
            if (data == null) return Result<string>.Failure("Registration details are required.", ErrorCode.BadRequest);
            return await drafts.CompleteAsync(Token, data, cor, orcr, motor, ct);
        });
    [HttpDelete("session")]
    public Task<ActionResult<Result<bool>>> Cancel(CancellationToken ct) => Run(async () =>
    { await drafts.CancelAsync(Token, ct); return Result<bool>.Success(true); });
    private async Task<ActionResult<Result<T>>> Run<T>(Func<Task<Result<T>>> action)
    {
        try { return this.ToActionResult(await action()); }
        catch (DocumentValidationException ex) { return BadRequest(Result<T>.Failure(ex.Message, ErrorCode.BadRequest)); }
    }
}
