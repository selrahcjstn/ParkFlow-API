using MediatR;
using ParkFlow.Application.Common;
using System;

namespace ParkFlow.Application.Features.Auth.Commands.UpdateMicrosoftIdentity;

public record UpdateMicrosoftIdentityCommand(
    Guid UserId,
    string NewEmail,
    string NewExternalProviderId) : IRequest<Result<bool>>;
