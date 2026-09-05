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
        private readonly IEmailService? _emailService;

        public ReplyToFeedbackHandler(
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

            string userEmail = account?.PrimaryEmail ?? string.Empty;
            string userName = profile != null ? $"{profile.FirstName} {profile.LastName}".Trim() : "Valued User";

            if (!string.IsNullOrWhiteSpace(userEmail) && _emailService != null)
            {
                try
                {
                    string refId = feedback.Id.ToString().Length >= 8 ? feedback.Id.ToString().Substring(0, 8).ToUpper() : feedback.Id.ToString().ToUpper();
                    string categoryTitle = !string.IsNullOrEmpty(feedback.Category) 
                        ? char.ToUpper(feedback.Category[0]) + feedback.Category.Substring(1) 
                        : "General";
                    string subject = $"ParkFlow Support Reply: Feedback Inquiry #{refId}";

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
    .box--reply {{ background: #ecfdf5; border-left-color: #10b981; }}
    .box-label {{ font-size: 11px; font-weight: 800; text-transform: uppercase; letter-spacing: 0.8px; margin-bottom: 6px; }}
    .box-label--inquiry {{ color: #2563eb; }}
    .box-label--reply {{ color: #059669; }}
    .box-text {{ font-size: 14px; line-height: 1.6; color: #334155; white-space: pre-wrap; margin: 0; }}
    .notice-card {{ background: #eff6ff; border: 1px solid #bfdbfe; border-radius: 12px; padding: 14px 18px; margin-top: 24px; font-size: 13px; color: #1e40af; line-height: 1.5; }}
    .footer {{ background: #f8fafc; border-top: 1px solid #e2e8f0; padding: 20px; text-align: center; font-size: 12px; color: #94a3b8; }}
  </style>
</head>
<body>
  <div class='email-container'>
    <div class='header'>
      <h1>ParkFlow Administration</h1>
      <p>Official Feedback & Inquiry Response</p>
    </div>
    <div class='body-content'>
      <div class='greeting'>Hello {userName},</div>
      <div class='intro'>
        Thank you for submitting your feedback to ParkFlow regarding <strong>{categoryTitle}</strong>. An administrator has reviewed your message and responded below:
      </div>

      <div class='box'>
        <div class='box-label box-label--inquiry'>Your Feedback / Inquiry</div>
        <p class='box-text'>{feedback.Description}</p>
      </div>

      <div class='box box--reply'>
        <div class='box-label box-label--reply'>Administrator Response</div>
        <p class='box-text'>{request.ReplyMessage.Trim()}</p>
      </div>

      <div class='notice-card'>
        💡 <strong>Note on Inquiry Replies:</strong> If you have any follow-up questions or further details, feel free to reply directly or contact support. Our administration team processes inquiries within a few hours.
      </div>
    </div>
    <div class='footer'>
      &copy; {DateTime.UtcNow.Year} ParkFlow System Administration &bull; All Rights Reserved
    </div>
  </div>
</body>
</html>";

                    await _emailService.SendEmailAsync(userEmail, subject, htmlBody);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ReplyToFeedbackHandler] Email notification error: {ex.Message}");
                }
            }

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

            return Result<FeedbackDto>.Success(dto, "Reply sent and email notification dispatched successfully.");
        }
    }
}
