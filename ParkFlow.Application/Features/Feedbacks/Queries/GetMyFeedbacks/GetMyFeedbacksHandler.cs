using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Feedbacks.Queries.GetMyFeedbacks
{
    public class GetMyFeedbacksHandler : IRequestHandler<GetMyFeedbacksQuery, Result<IEnumerable<FeedbackDto>>>
    {
        private readonly IFeedbackRepository _feedbackRepository;
        private readonly IUserProfileRepository _userProfileRepository;
        private readonly IUserAccountRepository _userAccountRepository;

        public GetMyFeedbacksHandler(
            IFeedbackRepository feedbackRepository,
            IUserProfileRepository userProfileRepository,
            IUserAccountRepository userAccountRepository)
        {
            _feedbackRepository = feedbackRepository;
            _userProfileRepository = userProfileRepository;
            _userAccountRepository = userAccountRepository;
        }

        public async Task<Result<IEnumerable<FeedbackDto>>> Handle(GetMyFeedbacksQuery request, CancellationToken cancellationToken)
        {
            var feedbacks = await _feedbackRepository.GetByUserIdAsync(request.UserId);
            var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
            var account = await _userAccountRepository.GetByIdAsync(request.UserId);

            var fullName = profile != null ? $"{profile.FirstName} {profile.LastName}".Trim() : "User";
            var email = account?.PrimaryEmail ?? string.Empty;

            var dtos = feedbacks.Select(f => new FeedbackDto
            {
                Id = f.Id,
                UserId = f.UserId,
                FullName = fullName,
                Email = email,
                Category = f.Category,
                Rating = f.Rating,
                Description = f.Description,
                AttachmentUrl = f.AttachmentUrl,
                Status = f.Status,
                AdminNotes = f.AdminNotes,
                AdminReplyMessage = f.AdminReplyMessage,
                AdminRepliedAt = f.AdminRepliedAt,
                InvoiceNumber = f.InvoiceNumber,
                InvoiceAmount = f.InvoiceAmount,
                InvoiceDescription = f.InvoiceDescription,
                InvoiceStatus = f.InvoiceStatus,
                CreatedAt = f.CreatedAt
            }).ToList();

            return Result<IEnumerable<FeedbackDto>>.Success(dtos, "User feedbacks retrieved successfully.");
        }
    }
}
