using System;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Domain.Entities
{
    public class Feedback : BaseEntity
    {
        public Guid UserId { get; set; }
        public string Category { get; set; } = "general";
        public int Rating { get; set; }
        public string Description { get; set; } = null!;
        public string? AttachmentUrl { get; set; }
        public FeedbackStatus Status { get; set; } = FeedbackStatus.Pending;
        public string? AdminNotes { get; set; }

        // Admin Reply Fields
        public string? AdminReplyMessage { get; set; }
        public DateTime? AdminRepliedAt { get; set; }
        public string? InvoiceNumber { get; set; }
        public decimal? InvoiceAmount { get; set; }
        public string? InvoiceDescription { get; set; }
        public string? InvoiceStatus { get; set; }

        public UserProfile? UserProfile { get; set; }

        public Feedback() { }

        public Feedback(Guid userId, string category, int rating, string description, string? attachmentUrl = null)
        {
            UserId = userId;
            Category = string.IsNullOrWhiteSpace(category) ? "general" : category.ToLower();
            Rating = Math.Clamp(rating, 1, 5);
            Description = description;
            AttachmentUrl = attachmentUrl;
            Status = FeedbackStatus.Pending;
        }

        public void Reply(string replyMessage, bool markResolved = false)
        {
            AdminReplyMessage = replyMessage;
            AdminRepliedAt = DateTime.UtcNow;


            Status = markResolved ? FeedbackStatus.Resolved : FeedbackStatus.Reviewed;
        }
    }
}
