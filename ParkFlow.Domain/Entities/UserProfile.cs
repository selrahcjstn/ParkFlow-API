using ParkFlow.Domain.Entities;

public class UserProfile : BaseEntity
{
    // FK
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;


    // Profile Data
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string? ProfilePictureUrl { get; private set; }

    public Student? Student { get; set; }
    public Personnel? Personnel { get; set; }

    private UserProfile() { }

    public UserProfile(
        UserAccount userAccount,
        string firstName,
        string lastName,
        string? profilePictureUrl)
    {
        UserAccount = userAccount;
        UserAccountId = userAccount.Id;
        FirstName = firstName;
        LastName = lastName;
        ProfilePictureUrl = profilePictureUrl;
    }

    public void UpdateProfile(
        string? firstName,
        string? lastName,
        string? profilePictureUrl
        )
    {
        if (!string.IsNullOrWhiteSpace(firstName))
            FirstName = firstName;
        if (!string.IsNullOrWhiteSpace(lastName))
            LastName = lastName;
        if (!string.IsNullOrWhiteSpace(profilePictureUrl))
            ProfilePictureUrl = profilePictureUrl;
    }
}