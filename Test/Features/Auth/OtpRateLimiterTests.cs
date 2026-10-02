using System;
using System.Threading;
using ParkFlow.Infrastructure.Services;
using Xunit;

namespace Test.Features.Auth;

public class OtpRateLimiterTests
{
    [Fact]
    public void CheckRateLimit_FirstRequest_ShouldBeAllowed()
    {
        var limiter = new OtpRateLimiter();
        var email = "user1@example.com";

        var (isAllowed, errorMessage, retrySeconds) = limiter.CheckRateLimit(email);

        Assert.True(isAllowed);
        Assert.Null(errorMessage);
        Assert.Equal(0, retrySeconds);
    }

    [Fact]
    public void CheckRateLimit_ImmediateSecondRequest_ShouldBeDeniedDueToCooldown()
    {
        var limiter = new OtpRateLimiter();
        var email = "user2@example.com";

        limiter.RecordOtpDispatched(email);

        var (isAllowed, errorMessage, retrySeconds) = limiter.CheckRateLimit(email);

        Assert.False(isAllowed);
        Assert.NotNull(errorMessage);
        Assert.Contains("seconds before requesting a new verification code", errorMessage);
        Assert.True(retrySeconds > 0 && retrySeconds <= 60);
    }

    [Fact]
    public void CheckRateLimit_ExceedingWindowLimit_ShouldBeDenied()
    {
        var limiter = new OtpRateLimiter();
        var email = "user3@example.com";

        // Record 5 requests
        for (int i = 0; i < 5; i++)
        {
            limiter.RecordOtpDispatched(email);
        }

        var (isAllowed, errorMessage, retrySeconds) = limiter.CheckRateLimit(email);

        Assert.False(isAllowed);
        Assert.NotNull(errorMessage);
        Assert.True(retrySeconds > 0);
    }

    [Fact]
    public void CheckRateLimit_DifferentEmails_ShouldNotInterfere()
    {
        var limiter = new OtpRateLimiter();
        var email1 = "alpha@example.com";
        var email2 = "beta@example.com";

        limiter.RecordOtpDispatched(email1);

        var (isAllowed1, _, _) = limiter.CheckRateLimit(email1);
        var (isAllowed2, _, _) = limiter.CheckRateLimit(email2);

        Assert.False(isAllowed1);
        Assert.True(isAllowed2);
    }
}
