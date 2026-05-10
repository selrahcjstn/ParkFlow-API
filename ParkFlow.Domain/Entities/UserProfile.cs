using ParkFlow.Domain.Entities;

public class UserProfile : BaseEntity
{
    // FK
    public Guid UserAccountId { get; private set; }

    // Navigation
    public UserAccount UserAccount { get; private set; } = null!;

    // Profile Data
    public string Firstname { get; private set; } = null!;
    public string Lastname { get; private set; } = null!;
    public string? ProfilePictureUrl { get; private set; }

    private UserProfile() { }

    public UserProfile(
        Guid userAccountId,
        string firstname,
        string lastname,
        string? profilePictureUrl)
    {
        UserAccountId = userAccountId;
        Firstname = firstname;
        Lastname = lastname;
        ProfilePictureUrl = profilePictureUrl;
    }

    public void UpdateProfile(string? firstname, string? lastname, string? profilePictureUrl)
    {
        if (!string.IsNullOrWhiteSpace(firstname))
            Firstname = firstname;
        if (!string.IsNullOrWhiteSpace(lastname))
            Lastname = lastname;
        if (!string.IsNullOrWhiteSpace(profilePictureUrl))
            ProfilePictureUrl = profilePictureUrl;
    }
}