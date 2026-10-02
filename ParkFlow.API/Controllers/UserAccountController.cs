using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.Commands.AdminRequestResetOtp;
using ParkFlow.Application.Features.Users.Commands.ForgotPasswordUserAccount;
using ParkFlow.Application.Features.Users.Commands.LoginUserAccount;
using ParkFlow.Application.Features.Users.Commands.MicrosoftAuthUserAccount;
using ParkFlow.Application.Features.Users.Commands.ResetPasswordUserAccount;
using ParkFlow.Application.Features.Users.Commands.SendTempPassword;
using ParkFlow.Application.Features.Users.Commands.UpdateUserAccountAdmin;
using ParkFlow.Application.Features.Users.Commands.UpdatePhoneNumber;
using ParkFlow.Application.Features.Users.Commands.SetPrimaryEmail;
using ParkFlow.Application.Features.Users.Commands.VerifyResetPasswordCode;
using ParkFlow.Application.Features.Users.Queries.GetUserById;
using ParkFlow.Application.Features.Users.Queries.GetUserCredentials;
using ParkFlow.Application.Features.Users.Queries.GetUsersList;
using ParkFlow.Application.Features.Users.Commands.DeleteUserAccount;
using ParkFlow.Application.Features.Users.Commands.UpdateUserStatus;
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
        [HttpGet("credentials")]
        public async Task<ActionResult<Result<UserCredentialsDto>>> GetCredentials()
        {
            var userId = _userContext.GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(Result<UserCredentialsDto>.Failure("User not identified.", ErrorCode.Unauthorized));

            var result = await _mediator.Send(new GetUserCredentialsQuery(userId));
            return this.ToActionResult(result);
        }

        [Authorize]
        [HttpPut("primary-email")]
        public async Task<ActionResult<Result<bool>>> SetPrimaryEmail([FromBody] SetPrimaryEmailRequest request)
        {
            var userId = _userContext.GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(Result<bool>.Failure(false, "User not identified.", ErrorCode.Unauthorized));

            var command = new SetPrimaryEmailCommand(userId, request.Email);
            var result = await _mediator.Send(command);
            return this.ToActionResult(result);
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
            return this.ToActionResult(result);
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
            return this.ToActionResult(result);
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

        [Authorize]
        [HttpPost("admin-request-reset-otp")]
        public async Task<ActionResult<Result<string>>> AdminRequestResetOtp([FromBody] AdminRequestResetOtpRequestDTO request)
        {
            var adminUserId = _userContext.GetUserId();
            var command = new AdminRequestResetOtpCommand(adminUserId, request.TargetEmail, request.AdminEmail);

            var result = await _mediator.Send(command);
            if (result.IsSuccess)
                return Ok(result);

            return result.ErrorCode == ErrorCode.NotFound
                ? NotFound(result)
                : BadRequest(result);
        }

        [HttpPost("verify-reset-code")]
        public async Task<ActionResult<Result<string>>> VerifyResetCode(VerifyResetPasswordCodeRequest request)
        {
            var command = new VerifyResetPasswordCodeCommand(request.Email, request.Code);
            var result = await _mediator.Send(command);
            if (result.IsSuccess)
                return Ok(result);

            if (result.ErrorCode == ErrorCode.NotFound)
                return NotFound(result);

            return result.ErrorCode == ErrorCode.Unauthorized
                ? Unauthorized(result)
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

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<Result<IEnumerable<UserWithDetailsDto>>>> GetUsers()
        {
            var result = await _mediator.Send(new GetUsersListQuery());
            return this.ToActionResult(result);
        }

        [Authorize]
        [HttpPut("{id}/status")]
        public async Task<ActionResult<Result<Guid>>> UpdateStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
        {
            var command = new UpdateUserStatusCommand(id, request.Status);
            var result = await _mediator.Send(command);
            return this.ToActionResult(result);
        }

        [Authorize]
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Result<UserWithDetailsDto>>> GetUserById(Guid id)
        {
            var result = await _mediator.Send(new GetUserByIdQuery(id));
            return this.ToActionResult(result);
        }

        [Authorize]
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<Result<Guid>>> UpdateUser(Guid id, [FromBody] UpdateUserAccountAdminRequest request)
        {
            var command = new UpdateUserAccountAdminCommand(id, request);
            var result = await _mediator.Send(command);
            return this.ToActionResult(result);
        }

        [Authorize]
        [HttpPost("send-temp-password")]
        public async Task<ActionResult<Result<string>>> SendTempPassword([FromBody] SendTempPasswordRequestDTO request)
        {
            var command = new SendTempPasswordCommand(request.Email, request.TargetEmail, request.TemporaryPassword);
            var result = await _mediator.Send(command);
            return this.ToActionResult(result);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult<Result<bool>>> DeleteUser(Guid id)
        {
            var command = new DeleteUserAccountCommand(id);
            var result = await _mediator.Send(command);
            return this.ToActionResult(result);
        }

        [HttpPost("staff")]
        public async Task<ActionResult<Result<Guid>>> RegisterStaffAccount(
            [FromBody] CreateStaffAccountRequestDTO request,
            [FromHeader(Name = "X-Admin-Registration-Key")] string? registrationKey)
        {
            var email = request.Email ?? request.Account?.Email ?? string.Empty;
            var password = request.Password ?? request.Account?.Password ?? string.Empty;
            var phone = request.PhoneNumber ?? request.Account?.PhoneNumber ?? string.Empty;
            var first = request.FirstName ?? request.Profile?.FirstName ?? string.Empty;
            var last = request.LastName ?? request.Profile?.LastName ?? string.Empty;
            var middle = request.MiddleName ?? request.Profile?.MiddleName;

            var isGuard = string.Equals(request.AccountType, "Guard", StringComparison.OrdinalIgnoreCase);

            if (isGuard)
            {
                var guardCommand = new ParkFlow.Application.Features.RegisterGuard.Commands.CreateGuardAccount.CreateGuardAccountCommand(
                    new ParkFlow.Application.Features.RegisterGuard.Commands.CreateGuardAccount.AccountDto(email, password, phone),
                    new ParkFlow.Application.Features.RegisterGuard.Commands.CreateGuardAccount.ProfileDto(first, last, middle, null),
                    request.AssignedGate ?? 1
                );
                var guardResult = await _mediator.Send(guardCommand);
                return this.ToActionResult(guardResult);
            }
            else
            {
                Guid? currentUserId = null;
                try
                {
                    var userId = _userContext.GetUserId();
                    if (userId != Guid.Empty)
                        currentUserId = userId;
                }
                catch { }

                if (!currentUserId.HasValue || currentUserId == Guid.Empty)
                {
                    var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst("sub")?.Value
                        ?? User.FindFirst("user_id")?.Value;

                    if (Guid.TryParse(sub, out var parsedGuid) && parsedGuid != Guid.Empty)
                    {
                        currentUserId = parsedGuid;
                    }
                }

                var isSuperAdminClaim = User.IsInRole("SuperAdmin")
                    || User.HasClaim("role", "SuperAdmin")
                    || User.HasClaim("profile_type", "superadmin")
                    || string.Equals(User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value, "superadmin@parkflow.com", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(User.FindFirst("email")?.Value, "superadmin@parkflow.com", StringComparison.OrdinalIgnoreCase);

                var configuration = HttpContext.RequestServices.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
                var effectiveKey = registrationKey;
                if (string.IsNullOrWhiteSpace(effectiveKey) && isSuperAdminClaim && configuration != null)
                {
                    effectiveKey = configuration["AdminSettings:RegistrationKey"];
                }

                var roleLevel = request.RoleLevel == 0 ? ParkFlow.Domain.Enums.RoleLevel.SuperAdmin : ParkFlow.Domain.Enums.RoleLevel.Admin;

                var adminCommand = new ParkFlow.Application.Features.RegisterAdmin.Commands.CreateAdminAccount.CreateAdminAccountCommand(
                    new ParkFlow.Application.Features.RegisterAdmin.Commands.CreateAdminAccount.AccountDto(email, password, phone),
                    new ParkFlow.Application.Features.RegisterAdmin.Commands.CreateAdminAccount.ProfileDto(first, last, middle, null),
                    roleLevel,
                    RegistrationKey: effectiveKey,
                    CurrentUserId: currentUserId
                );
                var adminResult = await _mediator.Send(adminCommand);
                return this.ToActionResult(adminResult);
            }
        }
    }

    public record UpdateUserStatusRequest(string Status);
    public record AdminRequestResetOtpRequestDTO(string TargetEmail, string? AdminEmail);
    public record CreateStaffAccountRequestDTO(
        string? Email,
        string? Password,
        string? PhoneNumber,
        string? FirstName,
        string? LastName,
        string? MiddleName,
        string? AccountType,
        int? AssignedGate,
        int? RoleLevel,
        ParkFlow.Application.Features.RegisterAdmin.Commands.CreateAdminAccount.AccountDto? Account,
        ParkFlow.Application.Features.RegisterAdmin.Commands.CreateAdminAccount.ProfileDto? Profile
    );
}
