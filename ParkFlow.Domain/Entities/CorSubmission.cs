using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

public class CorSubmission : BaseEntity
{
    // FK
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;

    public string AcademicTerm { get; private set; } = null!;

    public string CorDocumentUrl { get; private set; } = null!;

    public CorVerificationStatus VerificationStatus { get; private set; }

    private CorSubmission() { }

    public CorSubmission(
        Guid userAccountId,
        string academicTerm,
        string corDocumentUrl)
    {
        UserAccountId = userAccountId;
        AcademicTerm = academicTerm;
        CorDocumentUrl = corDocumentUrl;


        VerificationStatus = CorVerificationStatus.NotSubmitted;
    }

    public void UpdateSubmission(
        string? academicTerm,
        string? corDocumentUrl,
        CorVerificationStatus? verificationStatus)
    {
        if (!string.IsNullOrWhiteSpace(academicTerm))
            AcademicTerm = academicTerm;

        if (!string.IsNullOrWhiteSpace(corDocumentUrl))
            CorDocumentUrl = corDocumentUrl;

        if (verificationStatus.HasValue)
            VerificationStatus = verificationStatus.Value;
    }
}