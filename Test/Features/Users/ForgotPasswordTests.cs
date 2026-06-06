using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.Commands.ForgotPasswordUserAccount;
using ParkFlow.Application.Features.Users.Commands.ResetPasswordUserAccount;
using ParkFlow.Application.Features.Users.Commands.VerifyResetPasswordCode;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Test.Features.Users;

public class FakePasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => "hashed_" + password;
    public bool VerifyPassword(string hashedPassword, string password) => hashedPassword == "hashed_" + password;
}

public class FakeEmailService : IEmailService
{
    public List<(string To, string Subject, string HtmlBody)> SentEmails { get; } = new();

    public Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        SentEmails.Add((to, subject, htmlBody));
        return Task.CompletedTask;
    }
}

public class ForgotPasswordTests
{
    private readonly FakeUserAccountRepository _userAccountRepository;
    private readonly ForgotPasswordUserAccountValidator _forgotValidator;
    private readonly ResetPasswordUserAccountValidator _resetValidator;
    private readonly VerifyResetPasswordCodeCommandValidator _verifyValidator;
    private readonly FakePasswordHasher _passwordHasher;
    private readonly FakeEmailService _emailService;

    public ForgotPasswordTests()
    {
        _userAccountRepository = new FakeUserAccountRepository();
        _forgotValidator = new ForgotPasswordUserAccountValidator();
        _resetValidator = new ResetPasswordUserAccountValidator();
        _verifyValidator = new VerifyResetPasswordCodeCommandValidator();
        _passwordHasher = new FakePasswordHasher();
        _emailService = new FakeEmailService();
    }

    [Fact]
    public async Task ForgotPassword_ShouldGenerate6DigitCodeAndSendEmail_WhenLinkedManualIdentityExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "manual@parkflow.com";
        var user = new UserAccount("hash", "+639000000000");

        var idProperty = typeof(BaseEntity).GetProperty("Id");
        idProperty?.SetValue(user, userId);

        var manualIdentity = AuthIdentity.CreateManual(userId, email, "hash", isPrimary: true);
        user.AuthIdentities.Add(manualIdentity);
        await _userAccountRepository.AddAsync(user);

        var command = new ForgotPasswordUserAccountCommand(email);
        var handler = new ForgotPasswordUserAccountHandler(_userAccountRepository, _emailService, _forgotValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(6, result.Data.Length);
        Assert.True(int.TryParse(result.Data, out _)); // must be 6-digit numeric

        Assert.NotNull(user.PasswordResetTokenHash);
        Assert.True(user.PasswordResetTokenExpiresAt > DateTime.UtcNow);

        // Code must expire in exactly 10 minutes (with small threshold for test execution duration)
        var timeDifference = user.PasswordResetTokenExpiresAt.Value - DateTime.UtcNow;
        Assert.True(timeDifference.TotalMinutes > 9.9 && timeDifference.TotalMinutes <= 10.0);

        // Verify email got sent with the 6-digit code
        var sentEmail = Assert.Single(_emailService.SentEmails);
        Assert.Equal(email, sentEmail.To);
        Assert.Contains(result.Data, sentEmail.HtmlBody);
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturnBadRequest_WhenNoManualIdentityLinked()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "microsoft@parkflow.com";
        var user = new UserAccount(null!, "+639000000000");

        var idProperty = typeof(BaseEntity).GetProperty("Id");
        idProperty?.SetValue(user, userId);

        var microsoftIdentity = AuthIdentity.CreateMicrosoft(userId, email, "ms-id");
        user.AuthIdentities.Add(microsoftIdentity);
        await _userAccountRepository.AddAsync(user);

        var command = new ForgotPasswordUserAccountCommand(email);
        var handler = new ForgotPasswordUserAccountHandler(_userAccountRepository, _emailService, _forgotValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
        Assert.Empty(_emailService.SentEmails);
    }

    [Fact]
    public async Task ResetPasswordFlow_ShouldVerifyCode_GenerateSecureToken_AndSuccessfullyResetPassword()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "resetflow@parkflow.com";
        var user = new UserAccount("old_hash", "+639000000000");

        var idProperty = typeof(BaseEntity).GetProperty("Id");
        idProperty?.SetValue(user, userId);

        var manualIdentity = AuthIdentity.CreateManual(userId, email, "old_hash", isPrimary: true);
        user.AuthIdentities.Add(manualIdentity);
        await _userAccountRepository.AddAsync(user);

        // 1. Generate 6-digit code
        var forgotCommand = new ForgotPasswordUserAccountCommand(email);
        var forgotHandler = new ForgotPasswordUserAccountHandler(_userAccountRepository, _emailService, _forgotValidator);
        var forgotResult = await forgotHandler.Handle(forgotCommand, CancellationToken.None);
        var verificationCode = forgotResult.Data ?? throw new Exception("Code was null");

        // 2. Verify code and exchange it for a secure token
        var verifyCommand = new VerifyResetPasswordCodeCommand(email, verificationCode);
        var verifyHandler = new VerifyResetPasswordCodeCommandHandler(_userAccountRepository, _verifyValidator);
        var verifyResult = await verifyHandler.Handle(verifyCommand, CancellationToken.None);
        
        Assert.True(verifyResult.IsSuccess);
        var secureResetToken = verifyResult.Data ?? throw new Exception("Reset token was null");
        Assert.True(secureResetToken.Length > 20); // must be a secure random token, not the 6-digit code

        // 3. Reset password using the secure token
        var newPassword = "NewPassword123!";
        var resetCommand = new ResetPasswordUserAccountCommand(email, secureResetToken, newPassword);
        var resetHandler = new ResetPasswordUserAccountHandler(_userAccountRepository, _resetValidator, _passwordHasher);

        // Act
        var result = await resetHandler.Handle(resetCommand, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Data);

        var expectedHash = _passwordHasher.HashPassword(newPassword);
        Assert.Equal(expectedHash, user.PasswordHash);
        Assert.Equal(expectedHash, manualIdentity.PasswordHash);
        Assert.Null(user.PasswordResetTokenHash);
        Assert.Null(user.PasswordResetTokenExpiresAt);
        
        // New password should be in history
        Assert.Contains(user.PasswordHistories, h => h.PasswordHash == expectedHash);
    }

    [Fact]
    public async Task VerifyResetCode_ShouldReturnUnauthorized_WhenCodeIsExpired()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "expiredcode@parkflow.com";
        var user = new UserAccount("old_hash", "+639000000000");

        var idProperty = typeof(BaseEntity).GetProperty("Id");
        idProperty?.SetValue(user, userId);

        var manualIdentity = AuthIdentity.CreateManual(userId, email, "old_hash", isPrimary: true);
        user.AuthIdentities.Add(manualIdentity);
        await _userAccountRepository.AddAsync(user);

        // Generate 6-digit code
        var forgotCommand = new ForgotPasswordUserAccountCommand(email);
        var forgotHandler = new ForgotPasswordUserAccountHandler(_userAccountRepository, _emailService, _forgotValidator);
        var forgotResult = await forgotHandler.Handle(forgotCommand, CancellationToken.None);
        var verificationCode = forgotResult.Data ?? throw new Exception("Code was null");

        // Force expiration
        var expiresAtField = typeof(UserAccount).GetProperty("PasswordResetTokenExpiresAt");
        expiresAtField?.SetValue(user, DateTime.UtcNow.AddMinutes(-5));

        // Verify code
        var verifyCommand = new VerifyResetPasswordCodeCommand(email, verificationCode);
        var verifyHandler = new VerifyResetPasswordCodeCommandHandler(_userAccountRepository, _verifyValidator);

        // Act
        var result = await verifyHandler.Handle(verifyCommand, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Unauthorized, result.ErrorCode);
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenReusingOldPassword()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "reuse@parkflow.com";
        var oldPassword = "OldSecretPassword123!";
        var oldPasswordHash = _passwordHasher.HashPassword(oldPassword);
        var user = new UserAccount(oldPasswordHash, "+639000000000");

        var idProperty = typeof(BaseEntity).GetProperty("Id");
        idProperty?.SetValue(user, userId);

        var manualIdentity = AuthIdentity.CreateManual(userId, email, oldPasswordHash, isPrimary: true);
        user.AuthIdentities.Add(manualIdentity);

        // Add old password to history
        user.PasswordHistories.Add(new PasswordHistory(userId, oldPasswordHash));
        await _userAccountRepository.AddAsync(user);

        // Generate 6-digit code
        var forgotCommand = new ForgotPasswordUserAccountCommand(email);
        var forgotHandler = new ForgotPasswordUserAccountHandler(_userAccountRepository, _emailService, _forgotValidator);
        var forgotResult = await forgotHandler.Handle(forgotCommand, CancellationToken.None);
        var verificationCode = forgotResult.Data ?? throw new Exception("Code was null");

        // Verify code and get reset token
        var verifyCommand = new VerifyResetPasswordCodeCommand(email, verificationCode);
        var verifyHandler = new VerifyResetPasswordCodeCommandHandler(_userAccountRepository, _verifyValidator);
        var verifyResult = await verifyHandler.Handle(verifyCommand, CancellationToken.None);
        var secureResetToken = verifyResult.Data ?? throw new Exception("Token was null");

        // Attempt reset with the same old password
        var resetCommand = new ResetPasswordUserAccountCommand(email, secureResetToken, oldPassword);
        var resetHandler = new ResetPasswordUserAccountHandler(_userAccountRepository, _resetValidator, _passwordHasher);

        // Act
        var result = await resetHandler.Handle(resetCommand, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
        Assert.Equal("You cannot reuse any of your previous passwords.", result.Message);
    }
}
