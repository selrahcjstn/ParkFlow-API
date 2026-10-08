using ParkFlow.Domain.Enums;

namespace ParkFlow.Domain.Entities;

public enum VisitSessionStatus
{
    Inside = 0,
    Completed = 1,
    Cancelled = 2
}

public class VisitSession : BaseEntity
{
    public const decimal ParkingFee = 20m;

    public Guid VisitorId { get; set; }
    public Visitor Visitor { get; set; } = null!;

    public DateTime EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }

    public string? Purpose { get; set; }
    public string? Destination { get; set; }

    public Guid? EntryGuardId { get; set; }
    public Guard? EntryGuard { get; set; }

    public Guid? ExitGuardId { get; set; }
    public Guard? ExitGuard { get; set; }

    public string? EntryGate { get; set; }
    public string? ExitGate { get; set; }

    public VisitSessionStatus Status { get; set; } = VisitSessionStatus.Inside;

    public VisitSession() { }

    public VisitSession(
        Guid visitorId,
        string? purpose = null,
        string? destination = null,
        Guid? entryGuardId = null,
        string? entryGate = null)
    {
        VisitorId = visitorId;
        Purpose = purpose?.Trim();
        Destination = destination?.Trim();
        EntryGuardId = entryGuardId;
        EntryGate = entryGate;
        EntryTime = DateTime.UtcNow;
        Status = VisitSessionStatus.Inside;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkExit(Guid? exitGuardId = null, string? exitGate = null)
    {
        ExitTime = DateTime.UtcNow;
        ExitGuardId = exitGuardId;
        ExitGate = exitGate;
        Status = VisitSessionStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }
}
