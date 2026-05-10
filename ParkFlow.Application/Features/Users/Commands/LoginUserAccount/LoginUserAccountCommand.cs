using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Users.Commands.LoginUserAccount;

public record LoginUserAccountCommand(
    string Email,
    string Password) : IRequest<Result<string>>;