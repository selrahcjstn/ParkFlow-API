using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Auth.Commands.LinkManualIdentity;

public record LinkManualIdentityCommand(
    Guid UserId,
    string Email,
    string Password) : IRequest<Result<Guid>>;
