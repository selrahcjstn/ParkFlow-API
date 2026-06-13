using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Auth.Commands.LinkManualIdentity;
using ParkFlow.Application.Features.Auth.Commands.UnlinkIdentity;
using ParkFlow.Application.Features.Auth.Commands.UpdateManualIdentity;
using ParkFlow.Application.Features.Auth.Commands.UpdateMicrosoftIdentity;
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

public class FakeUserAccountRepository : IUserAccountRepository
{
    public List<UserAccount> Users { get; } = new();

    public Task<UserAccount?> GetByEmailAsync(string email) => Task.FromResult(Users.FirstOrDefault(u => u.AuthIdentities.Any(i => i.Email != null && i.Email.Equals(email, StringComparison.OrdinalIgnoreCase))));
    public Task<UserAccount?> GetByAuthProviderExternalIdAsync(AuthProvider authProvider, string externalProviderId) =>
        Task.FromResult(Users.FirstOrDefault(u => u.AuthProvider == authProvider && u.ExternalProviderId == externalProviderId));
    public Task<UserAccount?> GetByIdAsync(Guid id) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
    public Task<UserAccount?> GetByPhoneNumberAsync(string phoneNumber) => Task.FromResult(Users.FirstOrDefault(u => u.PhoneNumber == phoneNumber));
    public Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null)
    {
        var q = Users.Where(u => u.AuthIdentities.Any(i => i.Email != null && i.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
        if (excludeUserId.HasValue) q = q.Where(u => u.Id != excludeUserId.Value);
        return Task.FromResult(q.Any());
    }
    public Task AddAsync(UserAccount user) { Users.Add(user); return Task.CompletedTask; }
    public Task<IEnumerable<UserAccount>> ListAllAsync() => Task.FromResult<IEnumerable<UserAccount>>(Users);
    public Task UpdateAsync(UserAccount user)
    {
        var existing = Users.FirstOrDefault(u => u.Id == user.Id);
        if (existing != null)
        {
            Users.Remove(existing);
            Users.Add(user);
        }
        return Task.CompletedTask;
    }
}

public class FakePasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => $"hashed:{password}";

    public bool VerifyPassword(string hashedPassword, string providedPassword) =>
        hashedPassword == HashPassword(providedPassword);
}

public class AccountLinkingTests
{
    private readonly FakeAuthIdentityRepository _authIdentityRepository;
    private readonly FakeUserAccountRepository _userAccountRepository;
    private readonly FakeEmailOtpRepository _emailOtpRepository;
    private readonly FakePasswordHasher _passwordHasher;
    private readonly LinkManualIdentityValidator _linkManualValidator;
    private readonly UnlinkIdentityCommandValidator _unlinkValidator;
    private readonly UpdateManualIdentityCommandValidator _updateManualValidator;
    private readonly UpdateMicrosoftIdentityCommandValidator _updateMicrosoftValidator;

    public AccountLinkingTests()
    {
        _authIdentityRepository = new FakeAuthIdentityRepository();
        _userAccountRepository = new FakeUserAccountRepository();
        _emailOtpRepository = new FakeEmailOtpRepository();
        _passwordHasher = new FakePasswordHasher();
        _linkManualValidator = new LinkManualIdentityValidator();
        _unlinkValidator = new UnlinkIdentityCommandValidator();
        _updateManualValidator = new UpdateManualIdentityCommandValidator();
        _updateMicrosoftValidator = new UpdateMicrosoftIdentityCommandValidator();
    }

    [Fact]
    public async Task LinkManualIdentityHandler_ShouldMarkIdentityVerified_WhenOtpWasAlreadyVerified()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "verified-link@parkflow.com";
        var user = UserAccount.CreateMicrosoft("ms-id-verified-link");

        var idField = typeof(BaseEntity).GetProperty("Id");
        idField?.SetValue(user, userId);
        await _userAccountRepository.AddAsync(user);

        var emailOtp = new EmailOtp(email, "123456", DateTime.UtcNow.AddMinutes(5));
        emailOtp.MarkAsUsed();
        await _emailOtpRepository.AddAsync(emailOtp);

        var command = new LinkManualIdentityCommand(userId, email, "Password123!");
        var handler = new LinkManualIdentityHandler(
            _userAccountRepository,
            _authIdentityRepository,
            _emailOtpRepository,
            _passwordHasher,
            _linkManualValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        var linkedIdentity = Assert.Single(_authIdentityRepository.Identities);
        Assert.True(linkedIdentity.IsVerified);

        var updatedUser = Assert.Single(_userAccountRepository.Users);
        Assert.Equal(AccountStatus.Verified, updatedUser.Status);
    }

    [Fact]
    public async Task UnlinkIdentityHandler_ShouldSuccessfullyUnlinkProvider_WhenMultipleProvidersExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "john@parkflow.com";

        var manualIdentity = AuthIdentity.CreateManual(userId, email, "passhash");
        var microsoftIdentity = AuthIdentity.CreateMicrosoft(userId, email, "ms-id-123");
        await _authIdentityRepository.AddAsync(manualIdentity);
        await _authIdentityRepository.AddAsync(microsoftIdentity);

        var command = new UnlinkIdentityCommand(userId, AuthProvider.Microsoft);
        var handler = new UnlinkIdentityCommandHandler(_authIdentityRepository, _unlinkValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);

        var remaining = Assert.Single(_authIdentityRepository.Identities);
        Assert.Equal(AuthProvider.Manual, remaining.Provider);
    }

    [Fact]
    public async Task UnlinkIdentityHandler_ShouldReturnConflict_WhenOnlyOneProviderExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "john@parkflow.com";

        var manualIdentity = AuthIdentity.CreateManual(userId, email, "passhash");
        await _authIdentityRepository.AddAsync(manualIdentity);

        var command = new UnlinkIdentityCommand(userId, AuthProvider.Manual);
        var handler = new UnlinkIdentityCommandHandler(_authIdentityRepository, _unlinkValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Single(_authIdentityRepository.Identities);
    }

    [Fact]
    public async Task UnlinkIdentityHandler_ShouldReturnNotFound_WhenProviderIsNotLinked()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "john@parkflow.com";

        var manualIdentity = AuthIdentity.CreateManual(userId, email, "passhash");
        await _authIdentityRepository.AddAsync(manualIdentity);

        var command = new UnlinkIdentityCommand(userId, AuthProvider.Microsoft);
        var handler = new UnlinkIdentityCommandHandler(_authIdentityRepository, _unlinkValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateManualIdentityHandler_ShouldSuccessfullyUpdateIdentityEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldEmail = "old@parkflow.com";
        var newEmail = "new@parkflow.com";

        var manualIdentity = AuthIdentity.CreateManual(userId, oldEmail, "passhash");
        await _authIdentityRepository.AddAsync(manualIdentity);

        var user = new UserAccount("passhash", null);
        var idField = typeof(BaseEntity).GetProperty("Id");
        idField?.SetValue(user, userId);
        await _userAccountRepository.AddAsync(user);

        var command = new UpdateManualIdentityCommand(userId, newEmail);
        var handler = new UpdateManualIdentityCommandHandler(
            _authIdentityRepository,
            _updateManualValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);

        var updatedIdentity = Assert.Single(_authIdentityRepository.Identities);
        Assert.Equal(newEmail, updatedIdentity.Email);

        Assert.Equal(newEmail, updatedIdentity.Email);
    }

    [Fact]
    public async Task UpdateManualIdentityHandler_ShouldReturnConflict_WhenEmailAlreadyLinkedToAnotherUser()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var email1 = "user1@parkflow.com";
        var email2 = "user2@parkflow.com";

        var identity1 = AuthIdentity.CreateManual(userId1, email1, "passhash");
        var identity2 = AuthIdentity.CreateManual(userId2, email2, "passhash");
        await _authIdentityRepository.AddAsync(identity1);
        await _authIdentityRepository.AddAsync(identity2);

        var command = new UpdateManualIdentityCommand(userId1, email2);
        var handler = new UpdateManualIdentityCommandHandler(
            _authIdentityRepository,
            _updateManualValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateMicrosoftIdentityHandler_ShouldSuccessfullyUpdateMicrosoftIdentity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldEmail = "ms_old@parkflow.com";
        var newEmail = "ms_new@parkflow.com";
        var oldExternalId = "ms-id-123";
        var newExternalId = "ms-id-456";

        var msIdentity = AuthIdentity.CreateMicrosoft(userId, oldEmail, oldExternalId);
        await _authIdentityRepository.AddAsync(msIdentity);

        var user = UserAccount.CreateMicrosoft(oldExternalId, null);
        var idField = typeof(BaseEntity).GetProperty("Id");
        idField?.SetValue(user, userId);
        await _userAccountRepository.AddAsync(user);

        var command = new UpdateMicrosoftIdentityCommand(userId, newEmail, newExternalId);
        var handler = new UpdateMicrosoftIdentityCommandHandler(
            _authIdentityRepository,
            _updateMicrosoftValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);

        var updatedIdentity = Assert.Single(_authIdentityRepository.Identities);
        Assert.Equal(newEmail, updatedIdentity.Email);
        Assert.Equal(newExternalId, updatedIdentity.ProviderId);

        Assert.Equal(newExternalId, updatedIdentity.ProviderId);
    }

    [Fact]
    public async Task UpdateMicrosoftIdentityHandler_ShouldReturnConflict_WhenNewExternalProviderIdIsAlreadyLinkedToAnotherUser()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var email1 = "user1@parkflow.com";
        var email2 = "user2@parkflow.com";
        var providerId1 = "ms-111";
        var providerId2 = "ms-222";

        var identity1 = AuthIdentity.CreateMicrosoft(userId1, email1, providerId1);
        var identity2 = AuthIdentity.CreateMicrosoft(userId2, email2, providerId2);
        await _authIdentityRepository.AddAsync(identity1);
        await _authIdentityRepository.AddAsync(identity2);

        var command = new UpdateMicrosoftIdentityCommand(userId1, "new_email@parkflow.com", providerId2);
        var handler = new UpdateMicrosoftIdentityCommandHandler(
            _authIdentityRepository,
            _updateMicrosoftValidator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
    }
}
