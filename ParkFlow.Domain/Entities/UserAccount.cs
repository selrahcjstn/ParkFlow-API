using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

public class UserAccount : BaseEntity
{
    // Core Account Details: 
    public string? PasswordHash { get; private set; }
    public string? PhoneNumber { get; private set; }
    public AuthProvider AuthProvider { get; private set; }
    public string? ExternalProviderId { get; private set; }
    public AccountStatus Status { get; private set; }
    public OnboardingStep OnboardingStep { get; private set; }
    
    public UserProfile? UserProfile { get; set; }
    public ICollection<AuthIdentity> AuthIdentities { get; private set; } = [];
    public ICollection<PasswordHistory> PasswordHistories { get; private set; } = [];
    public string? PrimaryEmail => AuthIdentities.FirstOrDefault(i => i.IsPrimary)?.Email
        ?? AuthIdentities.FirstOrDefault(i => i.Email != null)?.Email;

    public DateTime? PasswordLastUpdatedAt { get; private set; }

    public string? PasswordResetTokenHash { get; private set; }
    public DateTime? PasswordResetTokenExpiresAt { get; private set; }

    private UserAccount() { } // For EF Core

    public UserAccount(
        string passwordHash,
        string? phoneNumber)
    {
        PasswordHash = passwordHash;
        PhoneNumber = phoneNumber;
        AuthProvider = AuthProvider.Manual;
        ExternalProviderId = null;

        Status = AccountStatus.PendingVerification;
        OnboardingStep = OnboardingStep.Profile;
    }

    public static UserAccount CreateMicrosoft(
        string externalProviderId,
        string? phoneNumber = null)
    {
        return new UserAccount
        {
            PasswordHash = null,
            PhoneNumber = phoneNumber,
            AuthProvider = AuthProvider.Microsoft,
            ExternalProviderId = externalProviderId,
            Status = AccountStatus.PendingVerification,
            OnboardingStep = OnboardingStep.Profile
        };
    }

    // Domain Methods
    public void UpdateEmail(string? email, string? phoneNumber, Roles? role)
    {
        if (!string.IsNullOrWhiteSpace(phoneNumber))
            PhoneNumber = phoneNumber;
    }

    public void UpdateExternalProviderId(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("External provider ID cannot be empty.");

        ExternalProviderId = providerId;
    }

    public void Verify()
    {
        Status = AccountStatus.Verified;
    }

    public void UpdatePhoneNumber(string? phoneNumber)
    {
        if (!string.IsNullOrWhiteSpace(phoneNumber))
            PhoneNumber = phoneNumber;
    }

    public void UpdateOnboardingStep(OnboardingStep step)
    {
        if (step > OnboardingStep)
            OnboardingStep = step;
    }

    public void UpdatePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash cannot be empty.");

        PasswordHash = newPasswordHash;
        AuthProvider = AuthProvider.Manual;
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
