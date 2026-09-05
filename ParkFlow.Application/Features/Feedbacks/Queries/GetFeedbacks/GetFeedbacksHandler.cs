using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Feedbacks.Queries.GetFeedbacks
{
    public class GetFeedbacksHandler : IRequestHandler<GetFeedbacksQuery, Result<IEnumerable<FeedbackDto>>>
    {
        private readonly IFeedbackRepository _feedbackRepository;
        private readonly IUserAccountRepository _userAccountRepository;

        public GetFeedbacksHandler(
            IFeedbackRepository feedbackRepository,
            IUserAccountRepository userAccountRepository)
        {
            _feedbackRepository = feedbackRepository;
            _userAccountRepository = userAccountRepository;
        }

        public async Task<Result<IEnumerable<FeedbackDto>>> Handle(GetFeedbacksQuery request, CancellationToken cancellationToken)
        {
            var feedbacks = await _feedbackRepository.GetAllAsync(request.Category, request.Status);
            var dtos = new List<FeedbackDto>();

            foreach (var f in feedbacks)
            {
                var fullName = f.UserProfile != null ? $"{f.UserProfile.FirstName} {f.UserProfile.LastName}".Trim() : "User";
                var account = await _userAccountRepository.GetByIdAsync(f.UserId);

                dtos.Add(new FeedbackDto
                {
                    Id = f.Id,
                    UserId = f.UserId,
                    FullName = fullName,
                    Email = account?.PrimaryEmail ?? "N/A",
                    Category = f.Category,
                    Rating = f.Rating,
                    Description = f.Description,
                    AttachmentUrl = f.AttachmentUrl,
                    Status = f.Status,
                    AdminNotes = f.AdminNotes,
                    CreatedAt = f.CreatedAt
                });
            }

            return Result<IEnumerable<FeedbackDto>>.Success(dtos, "Feedbacks retrieved successfully.");
        }
    }
}
