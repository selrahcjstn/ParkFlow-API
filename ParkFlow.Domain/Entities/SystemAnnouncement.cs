using System;

namespace ParkFlow.Domain.Entities
{
    public class SystemAnnouncement : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = null!;
        public string IconType { get; set; } = "caution"; // caution, good_news, info, maintenance, urgent
        public bool IsActive { get; set; } = true;
        public Guid CreatedBy { get; set; }

        public SystemAnnouncement() { }

        public SystemAnnouncement(Guid createdBy, string title, string message, string iconType = "caution", bool isActive = true)
        {
            CreatedBy = createdBy;
            Title = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
            Message = message;
            IconType = string.IsNullOrWhiteSpace(iconType) ? "caution" : iconType.ToLower().Trim();
            IsActive = isActive;
        }

        public void Update(string title, string message, string iconType, bool isActive)
        {
            Title = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
            Message = message;
            IconType = string.IsNullOrWhiteSpace(iconType) ? "caution" : iconType.ToLower().Trim();
            IsActive = isActive;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
