using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Auth.Commands.LinkMicrosoftIdentity;

public record LinkMicrosoftIdentityCommand(
    Guid UserId,
    string ExternalProviderId,
    string Email,
    string? FirstName,
    string? LastName,
    string? DisplayName) : IRequest<Result<Guid>>;
