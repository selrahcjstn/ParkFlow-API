using MediatR;

namespace ParkFlow.Application.Features.Users.Commands.LoginUserAccount;

public record LoginUserAccountCommand(
    string Email,
    string Password) : IRequest<Guid>;