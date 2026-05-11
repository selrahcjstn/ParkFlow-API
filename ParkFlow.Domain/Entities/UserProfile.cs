using ParkFlow.Domain.Entities;

public class UserProfile : BaseEntity
{
    // FK
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;

    // Student ID or Staff ID
    public string IdCardNumber { get; private set; } = null!;

    // Profile Data
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string? ProfilePictureUrl { get; private set; }

    // Optional Student Account Details:
    public string? Course { get; private set; }
    public string? Section { get; private set; }
    public int? YearLevel { get; private set; }

    // Optional Staff Details:
    public string? Office { get; private set; }

    private UserProfile() { }

    public UserProfile(
        Guid userAccountId,
        string idCardNumber,
        string firstName,
        string lastName,
        string? profilePictureUrl,
        string? course,
        string? section,
        int? yearLevel,
        string? office)
    {
        UserAccountId = userAccountId;
        IdCardNumber = idCardNumber;
        FirstName = firstName;
        LastName = lastName;
        ProfilePictureUrl = profilePictureUrl;
        Course = course;
        Section = section;
        YearLevel = yearLevel;
        Office = office;
    }

    public void UpdateProfile(
        string? idCardNumber,
        string? firstName,
        string? lastName,
        string? profilePictureUrl,
        string? course,
        string? section,
        int? yearLevel,
        string? office)
    {
        if (!string.IsNullOrWhiteSpace(idCardNumber))
            IdCardNumber = idCardNumber;
        if (!string.IsNullOrWhiteSpace(firstName))
            FirstName = firstName;
        if (!string.IsNullOrWhiteSpace(lastName))
            LastName = lastName;
        if (!string.IsNullOrWhiteSpace(profilePictureUrl))
            ProfilePictureUrl = profilePictureUrl;
        if (!string.IsNullOrWhiteSpace(course))
            Course = course;
        if (!string.IsNullOrWhiteSpace(section))
            Section = section;
        if (yearLevel.HasValue)
            YearLevel = yearLevel;
        if (!string.IsNullOrWhiteSpace(office))
            Office = office;
    }
}