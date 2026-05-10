using ParkFlow.Domain.Shared.Base;
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

    // Domain Methods
    public void UpdateEmail(string? email, string? phoneNumber, Roles? role)
    {
        if(!string.IsNullOrWhiteSpace(email))
            Email = email;
        if(!string.IsNullOrWhiteSpace(phoneNumber))
            PhoneNumber = phoneNumber;
        if(role.HasValue)
            Roles = role.Value;
    }
}