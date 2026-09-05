using System;
using System.Collections.Generic;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;

namespace ParkFlow.Application.Features.Feedbacks.Queries.GetMyFeedbacks
{
    public record GetMyFeedbacksQuery(Guid UserId) : IRequest<Result<IEnumerable<FeedbackDto>>>;
}
