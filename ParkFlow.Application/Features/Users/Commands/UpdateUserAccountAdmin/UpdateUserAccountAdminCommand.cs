using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.DTOs;
using System;

namespace ParkFlow.Application.Features.Users.Commands.UpdateUserAccountAdmin;

public record UpdateUserAccountAdminCommand(
    Guid UserId,
    UpdateUserAccountAdminRequest Request
) : IRequest<Result<Guid>>;
