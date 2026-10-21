using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Auth.Commands.SendEmailOtp;

public record SendEmailOtpCommand(
    string Email,
    string? Purpose = null,
    Guid? RequestingUserId = null) : IRequest<Result<bool>>;
