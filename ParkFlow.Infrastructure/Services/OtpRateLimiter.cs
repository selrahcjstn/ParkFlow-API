using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Infrastructure.Services;

public class OtpRateLimiter : IOtpRateLimiter
{
    private class OtpRecord
    {
        public DateTime LastDispatchedAt { get; set; }
        public List<DateTime> DispatchTimestamps { get; } = new();
    }

    private readonly ConcurrentDictionary<string, OtpRecord> _records = new(StringComparer.OrdinalIgnoreCase);

    private readonly TimeSpan _cooldown = TimeSpan.FromSeconds(60);
    private readonly TimeSpan _slidingWindow = TimeSpan.FromMinutes(15);
    private const int MaxAttemptsPerWindow = 5;

    public (bool IsAllowed, string? ErrorMessage, int RetryAfterSeconds) CheckRateLimit(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return (true, null, 0);

        var normalizedKey = key.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        if (_records.TryGetValue(normalizedKey, out var record))
        {
            lock (record)
            {
                // 1. Check cooldown (60 seconds between OTP requests)
                var elapsedSinceLast = now - record.LastDispatchedAt;
                if (elapsedSinceLast < _cooldown)
                {
                    var remainingSeconds = (int)Math.Ceiling((_cooldown - elapsedSinceLast).TotalSeconds);
                    return (false, $"Please wait {remainingSeconds} seconds before requesting a new verification code.", remainingSeconds);
                }

                // 2. Check window quota (5 requests per 15 minutes)
                record.DispatchTimestamps.RemoveAll(t => now - t > _slidingWindow);
                if (record.DispatchTimestamps.Count >= MaxAttemptsPerWindow)
                {
                    var oldestInWindow = record.DispatchTimestamps.Min();
                    var windowRemaining = (int)Math.Ceiling((_slidingWindow - (now - oldestInWindow)).TotalSeconds);
                    var remainingMinutes = Math.Max(1, (int)Math.Ceiling(windowRemaining / 60.0));
                    return (false, $"Too many verification code requests for this email. Please wait {remainingMinutes} minute(s) before trying again.", windowRemaining);
                }
            }
        }

        return (true, null, 0);
    }

    public void RecordOtpDispatched(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var normalizedKey = key.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        var record = _records.GetOrAdd(normalizedKey, _ => new OtpRecord());
        lock (record)
        {
            record.LastDispatchedAt = now;
            record.DispatchTimestamps.RemoveAll(t => now - t > _slidingWindow);
            record.DispatchTimestamps.Add(now);
        }
    }
}
