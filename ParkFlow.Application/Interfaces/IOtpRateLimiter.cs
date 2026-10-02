namespace ParkFlow.Application.Interfaces;

public interface IOtpRateLimiter
{
    /// <summary>
    /// Checks if an OTP dispatch request is allowed for the specified key (e.g. email address).
    /// Returns (IsAllowed, ErrorMessage, RetryAfterSeconds).
    /// </summary>
    (bool IsAllowed, string? ErrorMessage, int RetryAfterSeconds) CheckRateLimit(string key);

    /// <summary>
    /// Records an OTP dispatch event for the specified key to update cooldown and window counters.
    /// </summary>
    void RecordOtpDispatched(string key);
}
