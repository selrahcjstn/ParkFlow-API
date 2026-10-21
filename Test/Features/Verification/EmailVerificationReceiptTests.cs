using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ParkFlow.Application.Features.Auth.Commands.RegisterManualAccount;
using ParkFlow.Domain.Enums;
using ParkFlow.Infrastructure.Security;
using Test.Features.Auth;
using Xunit;

namespace Test.Features.Verification;

public class EmailVerificationReceiptTests
{
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["Jwt:Key"] = "test-only-key-that-is-at-least-thirty-two-bytes", ["Jwt:Issuer"] = "parkflow-test", ["Jwt:Audience"] = "parkflow-api" }).Build();

    [Fact]
    public void ReceiptIsBoundToEmailAndCannotAuthenticateToTheApi()
    {
        var config = Configuration(); var receipt = new EmailVerificationReceipt(config);
        var token = receipt.Issue(" Student@Example.Test ");
        Assert.True(receipt.Validate("student@example.test", token));
        Assert.False(receipt.Validate("different@example.test", token));
        Assert.False(receipt.Validate("student@example.test", "tampered." + token));
        Assert.ThrowsAny<SecurityTokenValidationException>(() => new JwtSecurityTokenHandler().ValidateToken(token,
            new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "parkflow-test", ValidateAudience = true,
                ValidAudience = "parkflow-api", ValidateLifetime = true, ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)) }, out _));
    }

    [Fact]
    public void ExpiredReceiptsCannotVerifyEmail()
    {
        var config = Configuration();
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("parkflow-test", "parkflow:email-verification",
            [new Claim("email", "student@example.test"), new Claim("purpose", "email_verification")],
            DateTime.UtcNow.AddMinutes(-20), DateTime.UtcNow.AddMinutes(-10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256)));
        Assert.False(new EmailVerificationReceipt(config).Validate("student@example.test", token));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PreRegistrationOtpReceiptVerifiesEmailWhileAccountRemainsPending(bool valid)
    {
        var users = new FakeUserAccountRepository(); var identities = new RegRoleAuthIdentityRepository();
        var receipts = new EmailVerificationReceipt(Configuration());
        var handler = new RegisterManualAccountHandler(users, identities, new RegRoleUserProfileRepository(), new RegRoleStudentRepository(),
            new RegRolePersonnelRepository(), new RegRoleGuardRepository(), new FakePasswordHasher(), new FakeJwtService(),
            new RegisterManualAccountValidator(), emailVerificationReceipt: receipts);
        var result = await handler.Handle(new RegisterManualAccountCommand("student@example.test", "Password123!",
            EmailVerificationToken: valid ? receipts.Issue("student@example.test") : receipts.Issue("wrong@example.test")), default);
        Assert.Equal(valid, result.IsSuccess);
        if (valid)
        {
            Assert.True(identities.Identities.Single().IsVerified);
            Assert.Equal(AccountStatus.PendingVerification, users.Users.Single().Status);
        }
        else { Assert.Empty(users.Users); Assert.Empty(identities.Identities); }
    }
}
