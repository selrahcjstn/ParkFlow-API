using MediatR;
using ParkFlow.Application.Common;
using System;

namespace ParkFlow.Application.Features.Users.Commands.UpdateUserStatus;

public record UpdateUserStatusCommand(Guid UserId, string Status) : IRequest<Result<Guid>>;
