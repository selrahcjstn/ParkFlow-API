using System;

namespace ParkFlow.Domain.Entities;

public class EmailOtp : BaseEntity
{
    public string Email { get; private set; } = null!;
    public string OtpCode { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public bool IsUsed { get; private set; }

    private EmailOtp() { } // For EF Core

    public EmailOtp(string email, string otpCode, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        if (string.IsNullOrWhiteSpace(otpCode))
            throw new ArgumentException("OTP code is required.", nameof(otpCode));

        if (expiresAt <= DateTime.UtcNow)
            throw new ArgumentException("Expiry time must be in the future.", nameof(expiresAt));

        Email = email;
        OtpCode = otpCode;
        ExpiresAt = expiresAt;
        IsUsed = false;
    }

    public void MarkAsUsed()
    {
        IsUsed = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
