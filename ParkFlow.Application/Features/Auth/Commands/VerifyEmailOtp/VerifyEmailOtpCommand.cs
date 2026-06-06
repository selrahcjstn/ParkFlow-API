using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Auth.Commands.VerifyEmailOtp;

public record VerifyEmailOtpCommand(
    string Email,
    string OtpCode,
    string? Purpose = null) : IRequest<Result<bool>>;
