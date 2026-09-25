using System;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;

namespace ParkFlow.Application.Features.Feedbacks.Commands.UpdateUserRating
{
    public record UpdateUserRatingCommand(Guid UserId, int Rating) : IRequest<Result<UserRatingDto>>;
}
