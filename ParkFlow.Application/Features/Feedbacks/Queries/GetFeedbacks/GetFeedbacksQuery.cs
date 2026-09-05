using System.Collections.Generic;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;

namespace ParkFlow.Application.Features.Feedbacks.Queries.GetFeedbacks
{
    public record GetFeedbacksQuery(
        string? Category = null,
        int? Status = null
    ) : IRequest<Result<IEnumerable<FeedbackDto>>>;
}
