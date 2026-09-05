using ParkFlow.Domain.Enums;

namespace ParkFlow.Domain.Entities;

public class ParkingReservation : BaseEntity
{
    public Guid UserId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;

    public string ReferenceNumber { get; private set; } = string.Empty;
    public DateTime ReservationDate { get; private set; }
    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    public ReservationStatus Status { get; private set; }
    public string? AdminNotes { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public Guid? ApprovedByAdminId { get; private set; }

    public ReservationType Type { get; private set; } = ReservationType.Normal;
    public Guid? VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }

    private ParkingReservation() { } // For EF Core

    public ParkingReservation(
        Guid userId,
        string referenceNumber,
        DateTime reservationDate,
        TimeSpan startTime,
        TimeSpan endTime,
        string reason,
        ReservationType type = ReservationType.Normal,
        Guid? vehicleId = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));

        if (string.IsNullOrWhiteSpace(referenceNumber))
            throw new ArgumentException("Reference number is required.", nameof(referenceNumber));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        UserId = userId;
        ReferenceNumber = referenceNumber;
        ReservationDate = DateTime.SpecifyKind(reservationDate.Date, DateTimeKind.Utc);
        StartTime = startTime;
        EndTime = endTime;
        Reason = reason.Trim();
        Status = ReservationStatus.Pending;
        Type = type;
        VehicleId = vehicleId;
    }

    public void SetAdminNotes(string? notes)
    {
        AdminNotes = notes;
    }

    private string? GetExistingNotifyTag()
    {
        if (string.IsNullOrWhiteSpace(AdminNotes)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(AdminNotes, @"\[NotifyEmail:(.*?)\]");
        return match.Success ? match.Value : null;
    }

    public void Approve(Guid adminId, string? notes = null)
    {
        if (Status != ReservationStatus.Pending)
            throw new InvalidOperationException("Only pending reservations can be approved.");

        Status = ReservationStatus.Approved;
        ApprovedByAdminId = adminId;
        ApprovedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        
        var existingTag = GetExistingNotifyTag();
        if (!string.IsNullOrWhiteSpace(notes))
        {
            AdminNotes = existingTag != null ? $"{existingTag} {notes.Trim()}" : notes.Trim();
        }
        else
        {
            AdminNotes = existingTag;
        }
    }

    public void Reject(Guid adminId, string? notes = null)
    {
        if (Status != ReservationStatus.Pending)
            throw new InvalidOperationException("Only pending reservations can be rejected.");

        Status = ReservationStatus.Rejected;
        ApprovedByAdminId = adminId;
        ApprovedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        var existingTag = GetExistingNotifyTag();
        if (!string.IsNullOrWhiteSpace(notes))
        {
            AdminNotes = existingTag != null ? $"{existingTag} {notes.Trim()}" : notes.Trim();
        }
        else
        {
            AdminNotes = existingTag;
        }
    }

    public void Cancel()
    {
        if (Status == ReservationStatus.Approved || Status == ReservationStatus.Rejected)
            throw new InvalidOperationException("Cannot cancel a reservation that has already been reviewed.");

        Status = ReservationStatus.Cancelled;
    }
}
