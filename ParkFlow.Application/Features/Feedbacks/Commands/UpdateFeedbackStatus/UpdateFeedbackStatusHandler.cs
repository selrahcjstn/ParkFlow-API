using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Feedbacks.Commands.UpdateFeedbackStatus
{
    public class UpdateFeedbackStatusHandler : IRequestHandler<UpdateFeedbackStatusCommand, Result<FeedbackDto>>
    {
        private readonly IFeedbackRepository _feedbackRepository;
        private readonly IUserAccountRepository _userAccountRepository;

        public UpdateFeedbackStatusHandler(
            IFeedbackRepository feedbackRepository,
            IUserAccountRepository userAccountRepository)
        {
            _feedbackRepository = feedbackRepository;
            _userAccountRepository = userAccountRepository;
        }

        public async Task<Result<FeedbackDto>> Handle(UpdateFeedbackStatusCommand request, CancellationToken cancellationToken)
        {
            var feedback = await _feedbackRepository.GetByIdAsync(request.Id);
            if (feedback == null)
            {
                return Result<FeedbackDto>.Failure("Feedback record not found.", ErrorCode.NotFound);
            }

            feedback.Status = request.Status;
            if (!string.IsNullOrWhiteSpace(request.AdminNotes))
            {
                feedback.AdminNotes = request.AdminNotes;
            }
            feedback.UpdatedAt = System.DateTime.UtcNow;

            await _feedbackRepository.UpdateAsync(feedback);

            var account = await _userAccountRepository.GetByIdAsync(feedback.UserId);
            var fullName = feedback.UserProfile != null ? $"{feedback.UserProfile.FirstName} {feedback.UserProfile.LastName}".Trim() : "User";

            var dto = new FeedbackDto
            {
                Id = feedback.Id,
                UserId = feedback.UserId,
                FullName = fullName,
                Email = account?.PrimaryEmail ?? "N/A",
                Category = feedback.Category,
                Rating = feedback.Rating,
                Description = feedback.Description,
                AttachmentUrl = feedback.AttachmentUrl,
                Status = feedback.Status,
                AdminNotes = feedback.AdminNotes,
                CreatedAt = feedback.CreatedAt
            };

            return Result<FeedbackDto>.Success(dto, "Feedback status updated successfully.");
        }
    }
}
