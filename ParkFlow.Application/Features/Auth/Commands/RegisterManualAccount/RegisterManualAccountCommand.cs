using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Auth.Commands.RegisterManualAccount;

public record RegisterManualAccountCommand(
    string Email,
    string Password) : IRequest<Result<string>>;
