using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Users.Commands.SendTempPassword;

public record SendTempPasswordCommand(
    string? Email,
    string? TargetEmail,
    string TemporaryPassword
) : IRequest<Result<string>>;
