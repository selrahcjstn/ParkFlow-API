using System;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;

namespace ParkFlow.Application.Features.Feedbacks.Queries.GetUserRating
{
    public record GetUserRatingQuery(Guid UserId) : IRequest<Result<UserRatingDto>>;
}
