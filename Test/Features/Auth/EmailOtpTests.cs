using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Auth.Commands.SendEmailOtp;
using ParkFlow.Application.Features.Auth.Commands.VerifyEmailOtp;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Test.Features.Auth;

public class FakeEmailOtpRepository : IEmailOtpRepository
{
    public List<EmailOtp> Otps { get; } = new();

    public Task AddAsync(EmailOtp emailOtp)
    {
        Otps.Add(emailOtp);
        return Task.CompletedTask;
    }

    public Task<EmailOtp?> GetLatestOtpByEmailAsync(string email)
    {
        return Task.FromResult(Otps
            .Where(o => o.Email == email)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault());
    }

    public Task UpdateAsync(EmailOtp emailOtp)
    {
        var existing = Otps.FirstOrDefault(o => o.Id == emailOtp.Id);
        if (existing != null)
        {
            Otps.Remove(existing);
            Otps.Add(emailOtp);
        }
        return Task.CompletedTask;
    }
}

public class FakeAuthIdentityRepository : IAuthIdentityRepository
{
    public List<AuthIdentity> Identities { get; } = new();

    public Task<AuthIdentity?> GetByProviderIdAsync(AuthProvider provider, string providerId)
    {
        return Task.FromResult(Identities.FirstOrDefault(i => i.Provider == provider && i.ProviderId == providerId));
    }

    public Task<AuthIdentity?> GetByEmailAsync(string email)
    {
        return Task.FromResult(Identities.FirstOrDefault(i => i.Email == email));
    }

    public Task<IEnumerable<AuthIdentity>> GetByAccountIdAsync(Guid accountId)
    {
        return Task.FromResult<IEnumerable<AuthIdentity>>(Identities.Where(i => i.UserAccountId == accountId).ToList());
    }

    public Task AddAsync(AuthIdentity identity)
    {
        Identities.Add(identity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AuthIdentity identity)
    {
        var existing = Identities.FirstOrDefault(i => i.Id == identity.Id);
        if (existing != null)
        {
            Identities.Remove(existing);
            Identities.Add(identity);
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AuthIdentity identity)
    {
        var existing = Identities.FirstOrDefault(i => i.Id == identity.Id);
        if (existing != null)
        {
            Identities.Remove(existing);
        }
        return Task.CompletedTask;
    }
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

public class EmailOtpTests
{
    private readonly FakeEmailOtpRepository _emailOtpRepository;
    private readonly FakeAuthIdentityRepository _authIdentityRepository;
    private readonly FakeUserAccountRepository _userAccountRepository;
    private readonly FakeEmailService _emailService;
    private readonly SendEmailOtpCommandValidator _sendValidator;
    private readonly VerifyEmailOtpCommandValidator _verifyValidator;

    public EmailOtpTests()
    {
        _emailOtpRepository = new FakeEmailOtpRepository();
        _authIdentityRepository = new FakeAuthIdentityRepository();
        _userAccountRepository = new FakeUserAccountRepository();
        _emailService = new FakeEmailService();
        _sendValidator = new SendEmailOtpCommandValidator();
        _verifyValidator = new VerifyEmailOtpCommandValidator();
    }

    [Fact]
    public async Task SendEmailOtpHandler_ShouldSuccessfullyGenerateSaveAndSendOtp()
    {
        // Arrange
        var email = "test@parkflow.com";
        var command = new SendEmailOtpCommand(email);
        var handler = new SendEmailOtpCommandHandler(_emailOtpRepository, _emailService, _sendValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);

        // Check stored OTP details
        var storedOtp = Assert.Single(_emailOtpRepository.Otps);
        Assert.Equal(email, storedOtp.Email);
        Assert.Equal(6, storedOtp.OtpCode.Length);
        Assert.True(int.TryParse(storedOtp.OtpCode, out _));
        Assert.False(storedOtp.IsUsed);
        Assert.True(storedOtp.ExpiresAt > DateTime.UtcNow);

        // Check email details
        var sentEmail = Assert.Single(_emailService.SentEmails);
        Assert.Equal(email, sentEmail.To);
        Assert.Contains(storedOtp.OtpCode, sentEmail.HtmlBody);
    }

    [Fact]
    public async Task SendEmailOtpHandler_ShouldReturnBadRequestIfEmailIsInvalid()
    {
        // Arrange
        var command = new SendEmailOtpCommand("invalid-email");
        var handler = new SendEmailOtpCommandHandler(_emailOtpRepository, _emailService, _sendValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
        Assert.Empty(_emailOtpRepository.Otps);
        Assert.Empty(_emailService.SentEmails);
    }

    [Fact]
    public async Task VerifyEmailOtpHandler_ShouldSuccessfullyVerifyValidOtpAndFlipAuthIdentityToVerified()
    {
        // Arrange
        var email = "verify@parkflow.com";
        var code = "123456";
        var expiresAt = DateTime.UtcNow.AddMinutes(5);
        var emailOtp = new EmailOtp(email, code, expiresAt);
        await _emailOtpRepository.AddAsync(emailOtp);

        // Create a user account and an unverified AuthIdentity matching the email
        var user = new UserAccount(email, "hashedPassword", "09171234567");
        await _userAccountRepository.AddAsync(user);

        var identity = AuthIdentity.CreateManual(user.Id, email, "hashedPassword");
        await _authIdentityRepository.AddAsync(identity);

        var command = new VerifyEmailOtpCommand(email, code);
        var handler = new VerifyEmailOtpCommandHandler(_emailOtpRepository, _authIdentityRepository, _userAccountRepository, _verifyValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);

        var updatedOtp = Assert.Single(_emailOtpRepository.Otps);
        Assert.True(updatedOtp.IsUsed);

        // Check that identity got flipped to verified
        var updatedIdentity = Assert.Single(_authIdentityRepository.Identities);
        Assert.True(updatedIdentity.IsVerified);

        // Check that user status got flipped to verified
        var updatedUser = Assert.Single(_userAccountRepository.Users);
        Assert.Equal(AccountStatus.Verified, updatedUser.Status);
    }

    [Fact]
    public async Task VerifyEmailOtpHandler_ShouldReturnNotFoundIfOtpDoesNotExist()
    {
        // Arrange
        var command = new VerifyEmailOtpCommand("no-otp@parkflow.com", "111111");
        var handler = new VerifyEmailOtpCommandHandler(_emailOtpRepository, _authIdentityRepository, _userAccountRepository, _verifyValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task VerifyEmailOtpHandler_ShouldReturnBadRequestIfOtpIsIncorrect()
    {
        // Arrange
        var email = "mismatch@parkflow.com";
        var correctCode = "123456";
        var wrongCode = "654321";
        var emailOtp = new EmailOtp(email, correctCode, DateTime.UtcNow.AddMinutes(5));
        await _emailOtpRepository.AddAsync(emailOtp);

        var command = new VerifyEmailOtpCommand(email, wrongCode);
        var handler = new VerifyEmailOtpCommandHandler(_emailOtpRepository, _authIdentityRepository, _userAccountRepository, _verifyValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
        Assert.False(emailOtp.IsUsed);
    }

    [Fact]
    public async Task VerifyEmailOtpHandler_ShouldReturnBadRequestIfOtpIsAlreadyUsed()
    {
        // Arrange
        var email = "already-used@parkflow.com";
        var code = "123456";
        var emailOtp = new EmailOtp(email, code, DateTime.UtcNow.AddMinutes(5));
        emailOtp.MarkAsUsed();
        await _emailOtpRepository.AddAsync(emailOtp);

        var command = new VerifyEmailOtpCommand(email, code);
        var handler = new VerifyEmailOtpCommandHandler(_emailOtpRepository, _authIdentityRepository, _userAccountRepository, _verifyValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
    }

    [Fact]
    public async Task VerifyEmailOtpHandler_ShouldReturnGoneIfOtpIsExpired()
    {
        // Arrange
        var email = "expired@parkflow.com";
        var code = "123456";
        var emailOtp = new EmailOtp(email, code, DateTime.UtcNow.AddMinutes(5));
        
        var expiresAtField = typeof(EmailOtp).GetProperty("ExpiresAt");
        expiresAtField?.SetValue(emailOtp, DateTime.UtcNow.AddMinutes(-5));

        await _emailOtpRepository.AddAsync(emailOtp);

        var command = new VerifyEmailOtpCommand(email, code);
        var handler = new VerifyEmailOtpCommandHandler(_emailOtpRepository, _authIdentityRepository, _userAccountRepository, _verifyValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Gone, result.ErrorCode);
        Assert.False(emailOtp.IsUsed);
    }

    [Fact]
    public async Task VerifyEmailOtpHandler_ShouldNotFlipStatusToVerifiedWhenPurposeIsChangePassword()
    {
        // Arrange
        var email = "changepassword@parkflow.com";
        var code = "123456";
        var expiresAt = DateTime.UtcNow.AddMinutes(5);
        var emailOtp = new EmailOtp(email, code, expiresAt);
        await _emailOtpRepository.AddAsync(emailOtp);

        // Create an unverified user account and AuthIdentity
        var user = new UserAccount(email, "hashedPassword", "09171234567");
        await _userAccountRepository.AddAsync(user);

        var identity = AuthIdentity.CreateManual(user.Id, email, "hashedPassword");
        await _authIdentityRepository.AddAsync(identity);

        // Command with non-verification purpose
        var command = new VerifyEmailOtpCommand(email, code, "ChangePassword");
        var handler = new VerifyEmailOtpCommandHandler(_emailOtpRepository, _authIdentityRepository, _userAccountRepository, _verifyValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);

        var updatedOtp = Assert.Single(_emailOtpRepository.Otps);
        Assert.True(updatedOtp.IsUsed);

        // Check that identity and user account did NOT flip to verified
        var updatedIdentity = Assert.Single(_authIdentityRepository.Identities);
        Assert.False(updatedIdentity.IsVerified);

        var updatedUser = Assert.Single(_userAccountRepository.Users);
        Assert.Equal(AccountStatus.PendingVerification, updatedUser.Status);
    }
}
