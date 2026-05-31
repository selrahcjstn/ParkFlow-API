using MediatR;
using ParkFlow.Application.Common;
using System;

namespace ParkFlow.Application.Features.Auth.Commands.UpdateManualIdentity;

public record UpdateManualIdentityCommand(
    Guid UserId,
    string NewEmail) : IRequest<Result<bool>>;
