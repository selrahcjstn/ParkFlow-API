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

    [HttpPost("register")]
    public async Task<ActionResult<Result<string>>> Register(RegisterManualRequest request)
    {
        var command = new RegisterManualAccountCommand(request.Email, request.Password);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

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

    [HttpPost("send-email-otp")]
    public async Task<ActionResult<Result<bool>>> SendEmailOtp(SendEmailOtpRequest request)
    {
        var command = new SendEmailOtpCommand(request.Email);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpPost("verify-email-otp")]
    public async Task<ActionResult<Result<bool>>> VerifyEmailOtp(VerifyEmailOtpRequest request)
    {
        var command = new VerifyEmailOtpCommand(request.Email, request.OtpCode);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
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
