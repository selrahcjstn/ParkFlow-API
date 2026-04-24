using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

public class UserAccount : BaseEntity
{
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public AccountStatus Status { get; private set; }
    public Roles Roles { get; private set; }

    public DateTime PasswordLastUpdatedAt { get; private set; } = DateTime.UtcNow;

    private UserAccount() { } // For EF Core

    public UserAccount(
        string email,
        string passwordHash,
        string phoneNumber,
        Roles role)
    {
        Email = email;
        PasswordHash = passwordHash;
        PhoneNumber = phoneNumber;

        Status = AccountStatus.Active;
        Roles = role;
    }
}