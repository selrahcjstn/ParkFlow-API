using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.DTOs;
using System;

namespace ParkFlow.Application.Features.Users.Queries.GetUserCredentials;

public record GetUserCredentialsQuery(Guid UserId) : IRequest<Result<UserCredentialsDto>>;
