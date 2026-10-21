using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Auth.Commands.LinkManualIdentity;
using ParkFlow.Application.Features.Auth.Commands.LinkMicrosoftIdentity;
using ParkFlow.Application.Features.Auth.Commands.RegisterManualAccount;
using ParkFlow.Application.Features.Auth.Commands.SendEmailOtp;
using ParkFlow.Application.Features.Auth.Commands.VerifyEmailOtp;
using ParkFlow.Application.Features.Auth.Commands.UnlinkIdentity;
using ParkFlow.Application.Features.Auth.Commands.UpdateManualIdentity;
using ParkFlow.Application.Features.Auth.Commands.UpdateMicrosoftIdentity;
using ParkFlow.Application.Features.Auth.DTOs;
using ParkFlow.Domain.Enums;
using ParkFlow.Application.Features.Users.Commands.MicrosoftAuthUserAccount;
using ParkFlow.Application.Features.Users.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.API.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;

    public AuthController(IMediator mediator, IUserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [HttpPost("register-manual")]
    public async Task<ActionResult<Result<string>>> Register(RegisterManualRequest request)
    {
        if (request.IsAdminCreated && (!User.HasClaim("parkflow_authentication_method", "pwd") || !(User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.HasClaim("profile_type", "admin") || User.HasClaim("profile_type", "superadmin"))))
            return Forbid();
        var command = new RegisterManualAccountCommand(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            request.MiddleName,
            request.PhoneNumber,
            request.Role,
            request.Status,
            request.Student,
            request.Personnel,
            request.Guard,
            request.IsAdminCreated,
            request.EmailVerificationToken);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("microsoft")]
    public async Task<ActionResult<Result<MicrosoftAuthResultDto>>> MicrosoftLogin(MicrosoftAuthRequestDTO request)
    {
        var command = new MicrosoftAuthUserAccountCommand(
            request.ExternalProviderId,
            request.Email,
            request.FirstName,
            request.LastName,
            request.DisplayName);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPost("link/manual")]
    public async Task<ActionResult<Result<Guid>>> LinkManual(LinkManualIdentityRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<Guid>.Failure("User not identified.", ErrorCode.Unauthorized));

        var command = new LinkManualIdentityCommand(userId, request.Email, request.Password);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPost("link/microsoft")]
    public async Task<ActionResult<Result<Guid>>> LinkMicrosoft(LinkMicrosoftIdentityRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<Guid>.Failure("User not identified.", ErrorCode.Unauthorized));

        var command = new LinkMicrosoftIdentityCommand(
            userId,
            request.ExternalProviderId,
            request.Email,
            request.FirstName,
            request.LastName,
            request.DisplayName);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
     }

    [AllowAnonymous]
    [HttpGet("check-email")]
    public async Task<ActionResult<Result<bool>>> CheckEmail(
        [FromQuery] string email,
        [FromServices] IAuthIdentityRepository authIdentityRepo,
        [FromServices] IUserAccountRepository userRepo)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(Result<bool>.Failure(false, "Email is required.", ErrorCode.BadRequest));

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var identity = await authIdentityRepo.GetByEmailAsync(normalizedEmail);
        if (identity != null)
            return Ok(Result<bool>.Failure(false, "This email is already in use.", ErrorCode.Conflict));

        var user = await userRepo.GetByEmailAsync(normalizedEmail);
        if (user != null)
            return Ok(Result<bool>.Failure(false, "This email is already in use.", ErrorCode.Conflict));

        return Ok(Result<bool>.Success(true, "Email is available."));
    }

    [AllowAnonymous]
    [HttpPost("send-email-otp")]
    public async Task<ActionResult<Result<bool>>> SendEmailOtp(SendEmailOtpRequest request)
    {
        var command = new SendEmailOtpCommand(request.Email);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("verify-email-otp")]
    public async Task<ActionResult<Result<bool>>> VerifyEmailOtp(VerifyEmailOtpRequest request)
    {
        var command = new VerifyEmailOtpCommand(request.Email, request.OtpCode, request.Purpose);
        var result = await _mediator.Send(command);
        if (result.IsSuccess && (string.IsNullOrEmpty(request.Purpose) || request.Purpose.Equals("Verification", StringComparison.OrdinalIgnoreCase)))
            return Ok(new { result.IsSuccess, result.Data, result.Message, result.ErrorCode,
                emailVerificationToken = HttpContext.RequestServices.GetRequiredService<IEmailVerificationReceipt>().Issue(request.Email) });
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPost("email-verification/send")]
    public async Task<ActionResult<Result<bool>>> SendOwnedEmailOtp(SendEmailOtpRequest request,
        [FromServices] IAuthIdentityRepository identities)
    {
        var identity = await identities.GetByEmailAsync(request.Email);
        if (identity?.UserAccountId != _userContext.GetUserId()) return Forbid();
        return this.ToActionResult(await _mediator.Send(new SendEmailOtpCommand(request.Email, RequestingUserId: _userContext.GetUserId())));
    }

    [Authorize]
    [HttpDelete("link/{provider}")]
    public async Task<ActionResult<Result<bool>>> Unlink(string provider)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<bool>.Failure(false, "User not identified.", ErrorCode.Unauthorized));

        if (!Enum.TryParse<AuthProvider>(provider, true, out var providerEnum))
        {
            return BadRequest(Result<bool>.Failure(false, "Invalid authentication provider.", ErrorCode.BadRequest));
        }

        var command = new UnlinkIdentityCommand(userId, providerEnum);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPut("link/manual")]
    public async Task<ActionResult<Result<bool>>> UpdateManualLink([FromBody] UpdateManualIdentityRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<bool>.Failure(false, "User not identified.", ErrorCode.Unauthorized));

        var command = new UpdateManualIdentityCommand(userId, request.NewEmail);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPut("link/microsoft")]
    public async Task<ActionResult<Result<bool>>> UpdateMicrosoftLink([FromBody] UpdateMicrosoftIdentityRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<bool>.Failure(false, "User not identified.", ErrorCode.Unauthorized));

        var command = new UpdateMicrosoftIdentityCommand(userId, request.NewEmail, request.NewExternalProviderId);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }
}
