using MediatR;
using ParkFlow.Application.Common;
using System;

namespace ParkFlow.Application.Features.Users.Commands.SetPrimaryEmail;

public record SetPrimaryEmailCommand(
    Guid UserId,
    string Email) : IRequest<Result<bool>>;
