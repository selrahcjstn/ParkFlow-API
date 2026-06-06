using System;

namespace ParkFlow.Domain.Entities;

public class PasswordHistory : BaseEntity
{
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    private PasswordHistory() { } // For EF Core

    public PasswordHistory(Guid userAccountId, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty.");

        UserAccountId = userAccountId;
        PasswordHash = passwordHash;
    }
}
