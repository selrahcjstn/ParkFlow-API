using System;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;

namespace ParkFlow.Application.Features.Feedbacks.Commands.CreateFeedback
{
    public record CreateFeedbackCommand(
        Guid UserId,
        string Category,
        int Rating,
        string Description,
        string? AttachmentUrl = null
    ) : IRequest<Result<FeedbackDto>>;
}
