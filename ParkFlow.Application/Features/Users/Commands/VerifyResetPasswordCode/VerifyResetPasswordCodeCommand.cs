using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Users.Commands.VerifyResetPasswordCode;

public record VerifyResetPasswordCodeCommand(
    string Email,
    string Code) : IRequest<Result<string>>;
