using System;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Feedbacks.DTOs
{
    public class FeedbackDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? AttachmentUrl { get; set; }
        public FeedbackStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public string? AdminNotes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
