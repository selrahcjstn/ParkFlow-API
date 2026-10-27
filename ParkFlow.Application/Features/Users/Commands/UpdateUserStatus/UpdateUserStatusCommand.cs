using MediatR;
using ParkFlow.Application.Common;
using System;

namespace ParkFlow.Application.Features.Users.Commands.UpdateUserStatus;

public record UpdateUserStatusResponse(Guid Id, string Status);
public record UpdateUserStatusCommand(Guid UserId, string Status, Guid ActorId = default) : IRequest<Result<UpdateUserStatusResponse>>;
