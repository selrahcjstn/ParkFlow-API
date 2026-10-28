using Xunit;

namespace Test.Features.Verification;

public class VerificationDatabaseSafetyTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("not a connection string", false)]
    [InlineData("Host=localhost;Database=parkflow_verification_test", true)]
    [InlineData("Host=127.0.0.1;Database=parkflow_verification_test", true)]
    [InlineData("Host=localhost;Database=postgres", false)]
    [InlineData("Host=example.rds.amazonaws.com;Database=parkflow_verification_test", false)]
    [InlineData("Host=shared.example;Database=parkflow_verification_test", false)]
    public void VerificationFixtureRefusesNonLocalOrNonDisposableTargets(string? connection, bool allowed)
    {
        Assert.Equal(allowed, PostgresVerificationFactAttribute.IsDisposableLocal(connection));
    }
}
