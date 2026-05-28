using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.Commands.ForgotPasswordUserAccount;
using ParkFlow.Application.Features.Users.Commands.LoginUserAccount;
using ParkFlow.Application.Features.Users.Commands.MicrosoftAuthUserAccount;
using ParkFlow.Application.Features.Users.Commands.ResetPasswordUserAccount;
using ParkFlow.Application.Features.Users.Commands.UpdatePhoneNumber;
using ParkFlow.Application.Features.Users.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.API.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UserAccountController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUserContext _userContext;

        public UserAccountController(IMediator mediator, IUserContext userContext)
        {
            _mediator = mediator;
            _userContext = userContext;
        }

        [Authorize]
        [HttpPatch("phone-number")]
        public async Task<ActionResult<Result<Guid>>> UpdatePhoneNumber([FromBody] UpdatePhoneNumberRequest request)
        {
            var userId = _userContext.GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(Result<Guid>.Failure("User not identified.", ErrorCode.Unauthorized));

            var command = new UpdatePhoneNumberCommand(userId, request.PhoneNumber);
            var result = await _mediator.Send(command);

            if (result.IsSuccess)
                return Ok(result);

            return result.ErrorCode == ErrorCode.NotFound
                ? NotFound(result)
                : BadRequest(result);
        }

        [HttpPost("login")]
        public async Task<ActionResult<Result<AuthResponse>>> Login(LoginRequestDTO request)
        {
            var command = new LoginUserAccountCommand(
                request.Email,
                request.Password
            );

            var result = await _mediator.Send(command);
            if (result.IsSuccess)
                return Ok(result);

            return result.ErrorCode == ErrorCode.Unauthorized
                ? Unauthorized(result)
                : BadRequest(result);
        }

        [HttpPost("login-microsoft")]
        public async Task<ActionResult<Result<MicrosoftAuthResultDto>>> LoginMicrosoft(MicrosoftAuthRequestDTO request)
        {
            var command = new MicrosoftAuthUserAccountCommand(
                request.ExternalProviderId,
                request.Email,
                request.FirstName,
                request.LastName,
                request.DisplayName);

            var result = await _mediator.Send(command);
            if (result.IsSuccess)
                return Ok(result);

            return result.ErrorCode == ErrorCode.Conflict
                ? Conflict(result)
                : BadRequest(result);
        }

        [HttpPost("forgot-password")]
        public async Task<ActionResult<Result<string>>> ForgotPassword(ForgotPasswordRequestDTO request)
        {
            var command = new ForgotPasswordUserAccountCommand(request.Email);

            var result = await _mediator.Send(command);
            if (result.IsSuccess)
                return Ok(result);

            return result.ErrorCode == ErrorCode.NotFound
                ? NotFound(result)
                : BadRequest(result);
        }

        [HttpPost("reset-password")]
        public async Task<ActionResult<Result<Guid>>> ResetPassword(ResetPasswordRequestDTO request)
        {
            var command = new ResetPasswordUserAccountCommand(
                request.Email,
                request.ResetToken,
                request.NewPassword);

            var result = await _mediator.Send(command);
            if (result.IsSuccess)
                return Ok(result);

            if (result.ErrorCode == ErrorCode.NotFound)
                return NotFound(result);

            return result.ErrorCode == ErrorCode.Unauthorized
                ? Unauthorized(result)
                : BadRequest(result);
        }
    }
}
