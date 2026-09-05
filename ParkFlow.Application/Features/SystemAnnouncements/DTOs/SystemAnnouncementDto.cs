using System;

namespace ParkFlow.Application.Features.SystemAnnouncements.DTOs
{
    public class SystemAnnouncementDto
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public string IconType { get; set; } = "caution"; // caution, good_news, info, maintenance, urgent
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
