using System;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Feedbacks.Commands.UpdateFeedbackStatus
{
    public record UpdateFeedbackStatusCommand(
        Guid Id,
        FeedbackStatus Status,
        string? AdminNotes = null
    ) : IRequest<Result<FeedbackDto>>;
}
