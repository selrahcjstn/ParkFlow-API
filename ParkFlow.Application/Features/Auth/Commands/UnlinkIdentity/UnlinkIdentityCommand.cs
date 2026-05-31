using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;
using System;

namespace ParkFlow.Application.Features.Auth.Commands.UnlinkIdentity;

public record UnlinkIdentityCommand(
    Guid UserId,
    AuthProvider Provider) : IRequest<Result<bool>>;
