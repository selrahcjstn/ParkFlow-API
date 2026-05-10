using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

public class UserAccount : BaseEntity
{
    // Core Account Details: 
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public AccountStatus Status { get; private set; }
    public Roles Roles { get; private set; }

    private UserProfile UserProfile { get; set; } = null!; // Navigation property

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

        Status = AccountStatus.PendingVerification;
        Roles = role;
    }

    // Domain Methods
    public void UpdateEmail(string? email, string? phoneNumber, Roles? role)
    {
        if (!string.IsNullOrWhiteSpace(email))
            Email = email;
        if (!string.IsNullOrWhiteSpace(phoneNumber))
            PhoneNumber = phoneNumber;
        if (role.HasValue)
            Roles = role.Value;
    }

    public void UpdatePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash cannot be empty.");

        PasswordHash = newPasswordHash;
        PasswordLastUpdatedAt = DateTime.UtcNow;
    }
}