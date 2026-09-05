using System;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;

namespace ParkFlow.Application.Features.Feedbacks.Commands.ReplyToFeedback
{
    public record ReplyToFeedbackCommand(
        Guid FeedbackId,
        string ReplyMessage,
        decimal? InvoiceAmount = null,
        string? InvoiceDescription = null,
        bool MarkResolved = false
    ) : IRequest<Result<FeedbackDto>>;
}
