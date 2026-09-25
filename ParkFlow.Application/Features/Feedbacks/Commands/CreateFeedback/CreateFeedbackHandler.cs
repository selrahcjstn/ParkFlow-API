using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Feedbacks.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Feedbacks.Commands.CreateFeedback
{
    public class CreateFeedbackHandler : IRequestHandler<CreateFeedbackCommand, Result<FeedbackDto>>
    {
        private readonly IFeedbackRepository _feedbackRepository;
        private readonly IUserProfileRepository _userProfileRepository;
        private readonly IUserAccountRepository _userAccountRepository;
        private readonly IEmailService? _emailService;

        public CreateFeedbackHandler(
            IFeedbackRepository feedbackRepository,
            IUserProfileRepository userProfileRepository,
            IUserAccountRepository userAccountRepository,
            IEmailService? emailService = null)
        {
            _feedbackRepository = feedbackRepository;
            _userProfileRepository = userProfileRepository;
            _userAccountRepository = userAccountRepository;
            _emailService = emailService;
        }

        public async Task<Result<FeedbackDto>> Handle(CreateFeedbackCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length < 15)
            {
                return Result<FeedbackDto>.Failure("Please provide a more detailed description (minimum 15 characters required).", ErrorCode.BadRequest);
            }

            // Anti-Spam & Rate Limiting Protections
            var userFeedbacks = (await _feedbackRepository.GetByUserIdAsync(request.UserId)).ToList();
            var existingRatingFeedback = userFeedbacks.FirstOrDefault(f => f.Rating >= 1 && f.Rating <= 5);

            int finalRating = request.Rating;
            if (finalRating < 1 || finalRating > 5)
            {
                if (existingRatingFeedback != null)
                {
                    finalRating = existingRatingFeedback.Rating;
                }
                else
                {
                    return Result<FeedbackDto>.Failure("Please select a satisfaction rating (1 to 5 stars).", ErrorCode.BadRequest);
                }
            }

            // 1. Cooldown check: 10-minute cooldown between any two submissions
            var latest = userFeedbacks.FirstOrDefault();
            if (latest != null && (DateTime.UtcNow - latest.CreatedAt).TotalMinutes < 10)
            {
                var remainingMinutes = (int)Math.Ceiling(10 - (DateTime.UtcNow - latest.CreatedAt).TotalMinutes);
                return Result<FeedbackDto>.Failure(
                    $"Please wait {remainingMinutes} more minute(s) before submitting another feedback or inquiry.",
                    ErrorCode.BadRequest);
            }

            // 2. Daily limit: Max 3 feedbacks within rolling 24 hours
            var countLast24Hours = userFeedbacks.Count(f => f.CreatedAt >= DateTime.UtcNow.AddHours(-24));
            if (countLast24Hours >= 3)
            {
                return Result<FeedbackDto>.Failure(
                    "You have reached the limit of 3 feedback submissions per day. Please try again tomorrow.",
                    ErrorCode.BadRequest);
            }

            // 3. Pending inquiries cap: Max 2 pending inquiries awaiting admin response
            var pendingCount = userFeedbacks.Count(f => f.Status == FeedbackStatus.Pending);
            if (pendingCount >= 2)
            {
                return Result<FeedbackDto>.Failure(
                    "You already have 2 pending inquiries awaiting administration response. Please wait until they are addressed.",
                    ErrorCode.BadRequest);
            }

            // 4. Duplicate content check: Disallow submitting identical content within 24 hours
            if (userFeedbacks.Any(f => f.Description.Trim().Equals(request.Description.Trim(), StringComparison.OrdinalIgnoreCase) && (DateTime.UtcNow - f.CreatedAt).TotalHours < 24))
            {
                return Result<FeedbackDto>.Failure(
                    "You have already submitted an inquiry with identical content recently. Please check your Inquiries & Replies tab.",
                    ErrorCode.BadRequest);
            }

            var feedback = new Feedback(
                request.UserId,
                request.Category,
                finalRating,
                request.Description.Trim(),
                request.AttachmentUrl
            );

            await _feedbackRepository.AddAsync(feedback);

            // Sync all user's previous feedbacks if rating was modified
            if (existingRatingFeedback != null && finalRating != existingRatingFeedback.Rating)
            {
                foreach (var f in userFeedbacks)
                {
                    f.Rating = finalRating;
                    await _feedbackRepository.UpdateAsync(f);
                }
            }

            var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
            var account = await _userAccountRepository.GetByIdAsync(request.UserId);

            var fullName = profile != null ? $"{profile.FirstName} {profile.LastName}".Trim() : "Valued User";
            var email = account?.PrimaryEmail ?? string.Empty;

            // Send automatic Thank-You confirmation email to user upon feedback submission
            if (!string.IsNullOrWhiteSpace(email) && _emailService != null)
            {
                try
                {
                    string refId = feedback.Id.ToString().Length >= 8 ? feedback.Id.ToString().Substring(0, 8).ToUpper() : feedback.Id.ToString().ToUpper();
                    string categoryTitle = !string.IsNullOrEmpty(feedback.Category) 
                        ? char.ToUpper(feedback.Category[0]) + feedback.Category.Substring(1) 
                        : "General";
                    string subject = $"Thank You for Your Feedback [Ref: #{refId}] - ParkFlow";

                    string htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'/>
  <style>
    body {{ font-family: 'Segoe UI', Helvetica, Arial, sans-serif; background-color: #f4f6f8; margin: 0; padding: 24px; color: #1e293b; }}
    .email-container {{ max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 16px; overflow: hidden; border: 1px solid #e2e8f0; box-shadow: 0 4px 20px rgba(0,0,0,0.06); }}
    .header {{ background: linear-gradient(135deg, #D22730 0%, #A81D24 100%); padding: 32px 24px; text-align: center; color: #ffffff; }}
    .header h1 {{ margin: 0; font-size: 24px; font-weight: 800; letter-spacing: -0.5px; }}
    .header p {{ margin: 6px 0 0; font-size: 13.5px; opacity: 0.9; font-weight: 500; }}
    .body-content {{ padding: 32px 28px; }}
    .greeting {{ font-size: 17px; font-weight: 700; color: #0f172a; margin-bottom: 14px; }}
    .intro {{ font-size: 14.5px; line-height: 1.6; color: #475569; margin-bottom: 24px; }}
    .box {{ background: #f8fafc; border-left: 4px solid #3b82f6; border-radius: 8px; padding: 16px 18px; margin-bottom: 20px; }}
    .box-label {{ font-size: 11px; font-weight: 800; text-transform: uppercase; letter-spacing: 0.8px; color: #2563eb; margin-bottom: 6px; }}
    .box-text {{ font-size: 14px; line-height: 1.6; color: #334155; white-space: pre-wrap; margin: 0; }}
    .notice-card {{ background: #ecfdf5; border: 1px solid #a7f3d0; border-radius: 12px; padding: 14px 18px; margin-top: 24px; font-size: 13px; color: #065f46; line-height: 1.5; }}
    .footer {{ background: #f8fafc; border-top: 1px solid #e2e8f0; padding: 20px; text-align: center; font-size: 12px; color: #94a3b8; }}
  </style>
</head>
<body>
  <div class='email-container'>
    <div class='header'>
      <h1>ParkFlow Administration</h1>
      <p>Feedback Submission Received</p>
    </div>
    <div class='body-content'>
      <div class='greeting'>Hello {fullName},</div>
      <div class='intro'>
        Thank you for submitting your feedback regarding <strong>{categoryTitle}</strong>. We have received your message and logged reference <strong>#{refId}</strong>.
      </div>

      <div class='box'>
        <div class='box-label'>Submitted Content</div>
        <p class='box-text'>{feedback.Description}</p>
      </div>

      <div class='notice-card'>
        🎉 <strong>Automatic Confirmation:</strong> Thank you for sharing your thoughts with ParkFlow! Our administration team is reviewing your message and will send an email response for your inquiry within a few hours.
      </div>
    </div>
    <div class='footer'>
      &copy; {DateTime.UtcNow.Year} ParkFlow System Administration &bull; All Rights Reserved
    </div>
  </div>
</body>
</html>";

                    await _emailService.SendEmailAsync(email, subject, htmlBody);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CreateFeedbackHandler] Automatic thank you email error: {ex.Message}");
                }
            }

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

            return Result<FeedbackDto>.Success(dto, "Feedback submitted successfully. An automatic confirmation email has been sent.");
        }
    }
}
