using ParkFlow.Domain.Entities;

public class UserProfile : BaseEntity
{
    // FK
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; set; } = null!;

    // Profile Data
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string? MiddleName { get; private set; }
    public string? ProfilePictureUrl { get; private set; }
    public string? DriversLicenseUrl { get; private set; }
    public ParkFlow.Domain.Enums.CorVerificationStatus LicenseStatus { get; private set; }
    public Guid? LicenseReviewedBy { get; private set; }
    public DateTime? LicenseReviewedAt { get; private set; }
    public string? LicenseRejectionReason { get; private set; }

    public void SubmitLicense(string url)
    {
        DriversLicenseUrl = url;
        LicenseStatus = ParkFlow.Domain.Enums.CorVerificationStatus.Pending;
        LicenseReviewedBy = null; LicenseReviewedAt = null; LicenseRejectionReason = null;
    }
    public void ReviewLicense(bool approved, Guid actor, string reason)
    {
        LicenseStatus = approved ? ParkFlow.Domain.Enums.CorVerificationStatus.Verified : ParkFlow.Domain.Enums.CorVerificationStatus.Rejected;
        LicenseReviewedBy = actor; LicenseReviewedAt = DateTime.UtcNow;
        LicenseRejectionReason = approved ? null : reason;
    }

    public Student? Student { get; set; }
    public Personnel? Personnel { get; set; }
    public Guard? Guard { get; set; }
    public Admin? Admin { get; set; }

    private UserProfile() { }

    public UserProfile(
        Guid userAccountId,
        string firstName,
        string lastName,
        string? middleName,
        string? profilePictureUrl)
    {
        UserAccountId = userAccountId;
        FirstName = firstName;
        LastName = lastName;
        MiddleName = middleName;
        ProfilePictureUrl = profilePictureUrl;
    }

    public void UpdateProfile(
        string? firstName,
        string? lastName,
        string? middleName,
        string? profilePictureUrl)
    {
        if (!string.IsNullOrWhiteSpace(firstName))
            FirstName = firstName;

        if (!string.IsNullOrWhiteSpace(lastName))
            LastName = lastName;

        MiddleName = middleName;

        if (profilePictureUrl != null)
            ProfilePictureUrl = string.IsNullOrWhiteSpace(profilePictureUrl) ? null : profilePictureUrl;
    }
}