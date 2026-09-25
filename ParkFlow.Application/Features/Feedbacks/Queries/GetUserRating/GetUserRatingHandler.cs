using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Feedbacks.Queries.GetUserRating
{
    public class GetUserRatingHandler : IRequestHandler<GetUserRatingQuery, Result<UserRatingDto>>
    {
        private readonly IFeedbackRepository _feedbackRepository;

        public GetUserRatingHandler(IFeedbackRepository feedbackRepository)
        {
            _feedbackRepository = feedbackRepository;
        }

        public async Task<Result<UserRatingDto>> Handle(GetUserRatingQuery request, CancellationToken cancellationToken)
        {
            var feedbacks = await _feedbackRepository.GetByUserIdAsync(request.UserId);
            var ratedFeedback = feedbacks.FirstOrDefault(f => f.Rating >= 1 && f.Rating <= 5);

            if (ratedFeedback != null)
            {
                return Result<UserRatingDto>.Success(
                    new UserRatingDto(ratedFeedback.Rating, ratedFeedback.CreatedAt, true),
                    "User rating retrieved successfully.");
            }

            return Result<UserRatingDto>.Success(
                new UserRatingDto(null, null, false),
                "User has not submitted a rating yet.");
        }
    }
}
