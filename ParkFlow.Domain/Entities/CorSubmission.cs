using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

public class CorSubmission : BaseEntity
{
    // FK
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;

    public string AcademicTerm { get; private set; } = null!;

    public string CorDocumentUrl { get; private set; } = null!;
    public string? OrcrDocumentUrl { get; private set; }
    public string? MotorPictureUrl { get; private set; }

    public CorVerificationStatus VerificationStatus { get; private set; }

    private CorSubmission() { }

    public CorSubmission(
        Guid userAccountId,
        string academicTerm,
        string corDocumentUrl,
        string? orcrDocumentUrl = null,
        string? motorPictureUrl = null)
    {
        UserAccountId = userAccountId;
        AcademicTerm = academicTerm;
        CorDocumentUrl = corDocumentUrl;
        OrcrDocumentUrl = orcrDocumentUrl;
        MotorPictureUrl = motorPictureUrl;

        VerificationStatus = CorVerificationStatus.Pending;
    }

    public void UpdateSubmission(
        string? academicTerm,
        string? corDocumentUrl,
        CorVerificationStatus? verificationStatus,
        string? orcrDocumentUrl = null,
        string? motorPictureUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(academicTerm))
            AcademicTerm = academicTerm;

        if (!string.IsNullOrWhiteSpace(corDocumentUrl))
            CorDocumentUrl = corDocumentUrl;

        if (!string.IsNullOrWhiteSpace(orcrDocumentUrl))
            OrcrDocumentUrl = orcrDocumentUrl;

        if (!string.IsNullOrWhiteSpace(motorPictureUrl))
            MotorPictureUrl = motorPictureUrl;

        if (verificationStatus.HasValue)
            VerificationStatus = verificationStatus.Value;
    }
}