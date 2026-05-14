using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

public class UserAccount : BaseEntity
{
    // Core Account Details: 
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public AccountStatus Status { get; private set; }

    public DateTime PasswordLastUpdatedAt { get; private set; }

    public string? PasswordResetTokenHash { get; private set; }
    public DateTime? PasswordResetTokenExpiresAt { get; private set; }

    private UserAccount() { } // For EF Core

    public UserAccount(
        string email,
        string passwordHash,
        string phoneNumber)
    {
        Email = email;
        PasswordHash = passwordHash;
        PhoneNumber = phoneNumber;

        Status = AccountStatus.PendingVerification;
    }

    // Domain Methods
    public void UpdateEmail(string? email, string? phoneNumber, Roles? role)
    {
        if (!string.IsNullOrWhiteSpace(email))
            Email = email;
        if (!string.IsNullOrWhiteSpace(phoneNumber))
            PhoneNumber = phoneNumber;
    }

    public void UpdatePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash cannot be empty.");

        PasswordHash = newPasswordHash;
        PasswordLastUpdatedAt = DateTime.UtcNow;
    }

    public void SetPasswordResetToken(string resetTokenHash, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(resetTokenHash))
            throw new ArgumentException("Reset token hash cannot be empty.");

        if (expiresAtUtc <= DateTime.UtcNow)
            throw new ArgumentException("Reset token expiry must be in the future.");

        PasswordResetTokenHash = resetTokenHash;
        PasswordResetTokenExpiresAt = expiresAtUtc;
    }

    public bool CanResetPasswordWithToken(string resetTokenHash, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(resetTokenHash))
            return false;

        if (PasswordResetTokenHash is null || PasswordResetTokenExpiresAt is null)
            return false;

        if (PasswordResetTokenExpiresAt.Value < utcNow)
            return false;

        return PasswordResetTokenHash == resetTokenHash;
    }

    public void ResetPasswordWithToken(string resetTokenHash, string newPasswordHash, DateTime utcNow)
    {
        if (!CanResetPasswordWithToken(resetTokenHash, utcNow))
            throw new InvalidOperationException("Invalid or expired reset token.");

        UpdatePassword(newPasswordHash);

        PasswordResetTokenHash = null;
        PasswordResetTokenExpiresAt = null;
    }
}