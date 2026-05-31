using ParkFlow.Domain.Enums;

namespace ParkFlow.Domain.Entities;

public class AuthIdentity : BaseEntity
{
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;

    public AuthProvider Provider { get; private set; }
    public string? Email { get; private set; }
    public string? ProviderId { get; private set; }
    public string? PasswordHash { get; private set; }
    public bool IsVerified { get; private set; }

    private AuthIdentity() { }

    public AuthIdentity(
        Guid userAccountId,
        AuthProvider provider,
        string? email,
        string? providerId,
        string? passwordHash,
        bool isVerified)
    {
        UserAccountId = userAccountId;
        Provider = provider;
        Email = email;
        ProviderId = providerId;
        PasswordHash = passwordHash;
        IsVerified = isVerified;
    }

    public static AuthIdentity CreateManual(Guid userAccountId, string email, string passwordHash)
    {
        return new AuthIdentity(userAccountId, AuthProvider.Manual, email, null, passwordHash, false);
    }

    public static AuthIdentity CreateMicrosoft(Guid userAccountId, string email, string providerId)
    {
        return new AuthIdentity(userAccountId, AuthProvider.Microsoft, email, providerId, null, true);
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty.");

        PasswordHash = passwordHash;
    }

    public void MarkVerified()
    {
        IsVerified = true;
    }

    public void UpdateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.");

        Email = email;
    }

    public void UpdateProviderId(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("Provider ID cannot be empty.");

        ProviderId = providerId;
    }
}
