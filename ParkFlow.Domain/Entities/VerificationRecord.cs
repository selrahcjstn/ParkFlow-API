namespace ParkFlow.Domain.Entities;

public enum VerificationType { Account, Cor, Vehicle, Schedule }
public enum VerificationState { Pending, Processing, PreVerified, Approved, NeedsReview, Rejected }
public enum VerificationMethod { Automatic, Manual }

// Each source revision gets a record. Decisions and replaced revisions remain auditable.
public class VerificationRecord : BaseEntity
{
    public Guid UserAccountId { get; set; }
    public Guid SubjectId { get; set; }
    public VerificationType Type { get; set; }
    public VerificationState Status { get; set; }
    public VerificationMethod? Method { get; set; }
    public string Fingerprint { get; set; } = "";
    public bool IsCurrent { get; set; } = true;
    public string Reason { get; set; } = "Queued for verification.";
    public string? OcrOutcome { get; set; }
    public string? ExtractedFieldsJson { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Guid? ReviewerId { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public int Version { get; set; }
}

public class VerificationPolicy
{
    public int Id { get; set; } = 1;
    public bool AccountAutomaticApproval { get; set; }
    public bool CorAutomaticApproval { get; set; }
    public bool VehicleAutomaticApproval { get; set; } // Retained for existing database/client compatibility; vehicles are always manual.
    public bool ScheduleAutomaticApproval { get; set; }
    // Empty terms intentionally fail closed until configured by the university.
    public string AcademicYear { get; set; } = "";
    public string Semester { get; set; } = "";
    public int MaxScheduleHoursPerDay { get; set; } = 8; // Legacy setting retained in storage; no daily hour cap is enforced.
    public int Version { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public bool Allows(VerificationType type) => type switch
    {
        VerificationType.Account => AccountAutomaticApproval,
        VerificationType.Cor => CorAutomaticApproval,
        VerificationType.Vehicle => false,
        VerificationType.Schedule => ScheduleAutomaticApproval,
        _ => false
    };
}

public class VerificationAudit : BaseEntity
{
    public Guid? VerificationRecordId { get; set; }
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Details { get; set; } = "";
}
