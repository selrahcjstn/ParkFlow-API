using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Feedbacks.Commands.CreateFeedback
{
    public class CreateFeedbackHandler : IRequestHandler<CreateFeedbackCommand, Result<FeedbackDto>>
    {
        private readonly IFeedbackRepository _feedbackRepository;
        private readonly IUserProfileRepository _userProfileRepository;
        private readonly IUserAccountRepository _userAccountRepository;

        public CreateFeedbackHandler(
            IFeedbackRepository feedbackRepository,
            IUserProfileRepository userProfileRepository,
            IUserAccountRepository userAccountRepository)
        {
            _feedbackRepository = feedbackRepository;
            _userProfileRepository = userProfileRepository;
            _userAccountRepository = userAccountRepository;
        }

        public async Task<Result<FeedbackDto>> Handle(CreateFeedbackCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return Result<FeedbackDto>.Failure("Description is required.", ErrorCode.BadRequest);
            }

            if (request.Rating < 1 || request.Rating > 5)
            {
                return Result<FeedbackDto>.Failure("Rating must be between 1 and 5 stars.", ErrorCode.BadRequest);
            }

            var feedback = new Feedback(
                request.UserId,
                request.Category,
                request.Rating,
                request.Description,
                request.AttachmentUrl
            );

            await _feedbackRepository.AddAsync(feedback);

            var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
            var account = await _userAccountRepository.GetByIdAsync(request.UserId);

            var fullName = profile != null ? $"{profile.FirstName} {profile.LastName}".Trim() : "Anonymous User";
            var email = account?.PrimaryEmail ?? string.Empty;

            var dto = new FeedbackDto
            {
                Id = feedback.Id,
                UserId = feedback.UserId,
                FullName = fullName,
                Email = email,
                Category = feedback.Category,
                Rating = feedback.Rating,
                Description = feedback.Description,
                AttachmentUrl = feedback.AttachmentUrl,
                Status = feedback.Status,
                AdminNotes = feedback.AdminNotes,
                CreatedAt = feedback.CreatedAt
            };

            return Result<FeedbackDto>.Success(dto, "Feedback submitted successfully.");
        }
    }
}
