using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Users.Commands.DeleteUserAccount;

public record DeleteUserAccountCommand(Guid Id) : IRequest<Result<bool>>;
