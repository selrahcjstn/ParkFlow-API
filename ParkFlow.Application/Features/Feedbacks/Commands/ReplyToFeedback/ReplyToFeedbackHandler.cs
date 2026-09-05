using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Feedbacks.Commands.ReplyToFeedback
{
    public class ReplyToFeedbackHandler : IRequestHandler<ReplyToFeedbackCommand, Result<FeedbackDto>>
    {
        private readonly IFeedbackRepository _feedbackRepository;
        private readonly IUserProfileRepository _userProfileRepository;
        private readonly IUserAccountRepository _userAccountRepository;

        public ReplyToFeedbackHandler(
            IFeedbackRepository feedbackRepository,
            IUserProfileRepository userProfileRepository,
            IUserAccountRepository userAccountRepository)
        {
            _feedbackRepository = feedbackRepository;
            _userProfileRepository = userProfileRepository;
            _userAccountRepository = userAccountRepository;
        }

        public async Task<Result<FeedbackDto>> Handle(ReplyToFeedbackCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ReplyMessage))
            {
                return Result<FeedbackDto>.Failure("Reply message is required.", ErrorCode.BadRequest);
            }

            var feedback = await _feedbackRepository.GetByIdAsync(request.FeedbackId);
            if (feedback == null)
            {
                return Result<FeedbackDto>.Failure("Feedback item not found.", ErrorCode.NotFound);
            }

            feedback.Reply(
                request.ReplyMessage.Trim(),
                request.InvoiceAmount,
                request.InvoiceDescription,
                request.MarkResolved
            );

            await _feedbackRepository.UpdateAsync(feedback);

            var profile = await _userProfileRepository.GetByUserIdAsync(feedback.UserId);
            var account = await _userAccountRepository.GetByIdAsync(feedback.UserId);

            var dto = new FeedbackDto
            {
                Id = feedback.Id,
                UserId = feedback.UserId,
                FullName = profile != null ? $"{profile.FirstName} {profile.LastName}".Trim() : "Anonymous User",
                Email = account?.PrimaryEmail ?? string.Empty,
                Category = feedback.Category,
                Rating = feedback.Rating,
                Description = feedback.Description,
                AttachmentUrl = feedback.AttachmentUrl,
                Status = feedback.Status,
                AdminNotes = feedback.AdminNotes,
                AdminReplyMessage = feedback.AdminReplyMessage,
                AdminRepliedAt = feedback.AdminRepliedAt,
                InvoiceNumber = feedback.InvoiceNumber,
                InvoiceAmount = feedback.InvoiceAmount,
                InvoiceDescription = feedback.InvoiceDescription,
                InvoiceStatus = feedback.InvoiceStatus,
                CreatedAt = feedback.CreatedAt
            };

            return Result<FeedbackDto>.Success(dto, "Reply sent and feedback status updated successfully.");
        }
    }
}
