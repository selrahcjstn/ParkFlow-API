using ParkFlow.Domain.Entities;
using ParkFlow.Application.Features.Verification;

namespace ParkFlow.Application.Interfaces;

public record VerificationSubject(Guid Id, Guid UserId, VerificationType Type, string Fingerprint,
    bool EmailVerified, bool DuplicateIdentifier, string? Identifier, string FirstName, string LastName,
    string? Course, bool IsPersonnel, string? DocumentUrl, string? AcademicTerm,
    string? PlateNumber, string? Brand, bool OwnershipDocumentsPresent, bool CorApproved,
    List<VerificationSchedule> Schedules)
{
    public bool Suspended { get; init; }
    public string? Email { get; init; }
    public string? OrcrUrl { get; init; }
    public string? VehiclePictureUrl { get; init; }
    public string? Section { get; init; }
    public int? YearLevel { get; init; }
}
public record VerificationSchedule(DayOfWeek Day, TimeSpan Start, TimeSpan End);
public record VerificationDecision(VerificationState Status, string Reason, string? OcrOutcome = null,
    string? ExtractedFieldsJson = null, bool TrustedEvidenceSucceeded = false)
{
    public int? PolicyVersion { get; init; }
}

public interface IVerificationStore
{
    Task<VerificationPolicy> GetPolicyAsync(CancellationToken ct);
    Task<bool> UpdatePolicyAsync(VerificationPolicy policy, Guid actor, CancellationToken ct);
    Task<VerificationSubject?> GetSubjectAsync(VerificationType type, Guid id, CancellationToken ct);
    Task<VerificationRecord?> EnqueueAsync(VerificationType type, Guid id, CancellationToken ct);
    Task<VerificationRecord?> ClaimAsync(TimeSpan lease, CancellationToken ct);
    Task<VerificationRecord?> ClaimCorAsync(Guid subjectId, TimeSpan lease, CancellationToken ct) => Task.FromResult<VerificationRecord?>(null);
    async Task<VerificationRecord?> FindCurrentCorAsync(Guid owner, CancellationToken ct) =>
        (await ListAsync(owner, false, ct)).FirstOrDefault(r => r.Type == VerificationType.Cor);
    Task<bool> CompleteAsync(VerificationRecord record, VerificationDecision decision, CancellationToken ct);
    Task<bool> RetryAsync(VerificationRecord record, int maxAttempts, CancellationToken ct);
    Task DeferPreparedCorAsync(VerificationRecord record, CancellationToken ct) => RetryAsync(record, 3, ct);
    Task RetireAsync(VerificationRecord record, CancellationToken ct);
    Task<bool> ReviewAsync(Guid id, int version, string fingerprint, VerificationDecision decision, Guid actor, CancellationToken ct);
    Task<List<VerificationRecord>> ListAsync(Guid? userId, bool history, CancellationToken ct);
    Task<VerificationRecord?> FindAsync(Guid id, CancellationToken ct);
    Task<List<VerificationAudit>> AuditAsync(Guid? recordId, CancellationToken ct);
    Task<bool> ConfirmExtractedSchedulesAsync(Guid recordId, int version, Guid owner, CancellationToken ct, List<VerificationSchedule>? corrected = null)
        => throw new NotSupportedException();
    Task<bool> ReviewVehicleManuallyAsync(Guid id, string fingerprint, VerificationState status, string reason, CancellationToken ct)
        => throw new NotSupportedException();
    Task<VerificationRecord?> FindCorExtractionByDocumentAsync(Guid owner, string documentUrl, CancellationToken ct)
        => Task.FromResult<VerificationRecord?>(null);
    Task<bool> StartCorInformationFetchAsync(Guid owner, Guid recordId, int version, CancellationToken ct) => throw new NotSupportedException();
    Task<bool> ConfirmStudentFromCorAsync(Guid owner, CorStudentConfirmation confirmation, CancellationToken ct)
        => throw new NotSupportedException();
    Task<bool> RequestCorScheduleReviewAsync(Guid owner, Guid recordId, int version, string reason, CancellationToken ct) => throw new NotSupportedException();
    Task<bool> CorrectCorScheduleAsync(Guid recordId, int version, Guid actor, List<CorWeekDay> week, string reason, CancellationToken ct) => throw new NotSupportedException();
}
