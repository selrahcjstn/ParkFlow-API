using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.DTOs;
using System.Collections.Generic;

namespace ParkFlow.Application.Features.Users.Queries.GetUsersList;

public record GetUsersListQuery() : IRequest<Result<IEnumerable<UserWithDetailsDto>>>;
