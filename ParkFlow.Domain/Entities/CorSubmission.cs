using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

public class CorSubmission : BaseEntity
{
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;

    public string AcademicTerm { get; private set; } = null!;

    public string CorDocumentUrl { get; private set; } = null!;

    public DateTime DateOfIncident { get; private set; }

    public CorVerificationStatus VerificationStatus { get; private set; }

    private CorSubmission() { }

    public CorSubmission(
        Guid userAccountId,
        string academicTerm,
        string corDocumentUrl,
        DateTime dateOfIncident)
    {
        UserAccountId = userAccountId;
        AcademicTerm = academicTerm;
        CorDocumentUrl = corDocumentUrl;
        DateOfIncident = dateOfIncident;

        VerificationStatus = CorVerificationStatus.NotSubmitted;
    }
}