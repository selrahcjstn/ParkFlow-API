using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Reservations.DTOs;

public class ParkingReservationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTime ReservationDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ReservationStatus Status { get; set; }
    public string? AdminNotes { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByAdminId { get; set; }
    public DateTime CreatedAt { get; set; }
}
