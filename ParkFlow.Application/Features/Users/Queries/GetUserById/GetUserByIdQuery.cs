using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.DTOs;
using System;

namespace ParkFlow.Application.Features.Users.Queries.GetUserById;

public record GetUserByIdQuery(Guid Id) : IRequest<Result<UserWithDetailsDto>>;
