using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Feedbacks.Commands.UpdateUserRating
{
    public class UpdateUserRatingHandler : IRequestHandler<UpdateUserRatingCommand, Result<UserRatingDto>>
    {
        private readonly IFeedbackRepository _feedbackRepository;

        public UpdateUserRatingHandler(IFeedbackRepository feedbackRepository)
        {
            _feedbackRepository = feedbackRepository;
        }

        public async Task<Result<UserRatingDto>> Handle(UpdateUserRatingCommand request, CancellationToken cancellationToken)
        {
            if (request.Rating < 1 || request.Rating > 5)
            {
                return Result<UserRatingDto>.Failure("Rating must be between 1 and 5 stars.", ErrorCode.BadRequest);
            }

            var feedbacks = (await _feedbackRepository.GetByUserIdAsync(request.UserId)).ToList();

            if (feedbacks.Any())
            {
                // Modify existing rating across user's feedbacks so their single rating is updated
                foreach (var f in feedbacks)
                {
                    f.Rating = request.Rating;
                    await _feedbackRepository.UpdateAsync(f);
                }
            }
            else
            {
                // Create initial rating entry
                var initial = new Feedback(
                    request.UserId,
                    "general",
                    request.Rating,
                    "App Satisfaction Rating",
                    null
                );
                await _feedbackRepository.AddAsync(initial);
            }

            return Result<UserRatingDto>.Success(
                new UserRatingDto(request.Rating, DateTime.UtcNow, true),
                "Rating modified successfully.");
        }
    }
}
