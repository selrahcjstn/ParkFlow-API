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

    public Student? Student { get; set; }
    public Personnel? Personnel { get; set; }
    public Guard? Guard { get; set; }

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

        if (!string.IsNullOrWhiteSpace(profilePictureUrl))
            ProfilePictureUrl = profilePictureUrl;
    }
}