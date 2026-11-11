using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.Commands.SetPrimaryEmail;
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

public class FakeAuthIdentityRepository : IAuthIdentityRepository
{
    public List<AuthIdentity> Identities { get; } = new();

    public Task<AuthIdentity?> GetByProviderIdAsync(AuthProvider provider, string providerId)
    {
        return Task.FromResult(Identities.FirstOrDefault(i => i.Provider == provider && i.ProviderId == providerId));
    }

    public Task<AuthIdentity?> GetByEmailAsync(string email)
    {
        return Task.FromResult(Identities.FirstOrDefault(i => i.Email != null && i.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
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

public class SetPrimaryEmailTests
{
    private readonly FakeUserAccountRepository _userAccountRepository;
    private readonly FakeAuthIdentityRepository _authIdentityRepository;
    private readonly SetPrimaryEmailCommandValidator _validator;

    public SetPrimaryEmailTests()
    {
        _userAccountRepository = new FakeUserAccountRepository();
        _authIdentityRepository = new FakeAuthIdentityRepository();
        _validator = new SetPrimaryEmailCommandValidator();
    }

    [Fact]
    public async Task SetPrimaryEmailHandler_ShouldSuccessfullyUpdatePrimaryIdentity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldEmail = "old@parkflow.com";
        var newEmail = "new@parkflow.com";
        var user = new UserAccount("hash", "+639000000000");

        var idProperty = typeof(BaseEntity).GetProperty("Id");
        idProperty?.SetValue(user, userId);

        var manualIdentity = AuthIdentity.CreateManual(userId, oldEmail, "hash", isPrimary: true);
        var microsoftIdentity = AuthIdentity.CreateMicrosoft(userId, newEmail, "ms-id");
        user.AuthIdentities.Add(manualIdentity);
        user.AuthIdentities.Add(microsoftIdentity);
        await _authIdentityRepository.AddAsync(manualIdentity);
        await _authIdentityRepository.AddAsync(microsoftIdentity);
        await _userAccountRepository.AddAsync(user);

        var command = new SetPrimaryEmailCommand(userId, newEmail);
        var handler = new SetPrimaryEmailCommandHandler(_userAccountRepository, _validator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);

        Assert.Equal(newEmail, _authIdentityRepository.Identities.Single(i => i.Email == newEmail).Email);
        Assert.True(_authIdentityRepository.Identities.Single(i => i.Email == newEmail).IsPrimary);
        Assert.False(_authIdentityRepository.Identities.Single(i => i.Email == oldEmail).IsPrimary);
    }

    private sealed class RecordingCache : ICacheService
    {
        public List<string> Removed { get; } = new();
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult(default(T));
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) { Removed.Add(key); return Task.CompletedTask; }
        public Task RemoveByPrefixAsync(string prefixKey, CancellationToken cancellationToken = default) { Removed.Add(prefixKey); return Task.CompletedTask; }
        public Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default) => factory();
    }

    [Fact]
    public async Task SetPrimaryEmailHandler_ClearsTheCachedProfileSoTheNewEmailIsShown()
    {
        var userId = Guid.NewGuid();
        var user = new UserAccount("hash", "+639000000000");
        typeof(BaseEntity).GetProperty("Id")?.SetValue(user, userId);
        user.AuthIdentities.Add(AuthIdentity.CreateManual(userId, "old@parkflow.com", "hash", isPrimary: true));
        user.AuthIdentities.Add(AuthIdentity.CreateMicrosoft(userId, "new@parkflow.com", "ms-id"));
        await _userAccountRepository.AddAsync(user);
        var cache = new RecordingCache();

        var result = await new SetPrimaryEmailCommandHandler(_userAccountRepository, _validator, cache)
            .Handle(new SetPrimaryEmailCommand(userId, "new@parkflow.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new@parkflow.com", user.PrimaryEmail);
        Assert.Contains(CacheKeys.UserProfile(userId), cache.Removed);
    }

    [Fact]
    public async Task SetPrimaryEmailHandler_ShouldReturnNotFoundIfEmailIsNotLinked()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "user@parkflow.com";
        var unlinkedEmail = "unlinked@parkflow.com";

        var user = new UserAccount("hash", "+639000000000");

        var idProperty = typeof(BaseEntity).GetProperty("Id");
        idProperty?.SetValue(user, userId);

        var manualIdentity = AuthIdentity.CreateManual(userId, email, "hash", isPrimary: true);
        user.AuthIdentities.Add(manualIdentity);
        await _userAccountRepository.AddAsync(user);
        await _authIdentityRepository.AddAsync(manualIdentity);

        var command = new SetPrimaryEmailCommand(userId, unlinkedEmail);
        var handler = new SetPrimaryEmailCommandHandler(_userAccountRepository, _validator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("Email is not linked to this account.", result.Message);
    }

    [Fact]
    public async Task SetPrimaryEmailHandler_ShouldReturnNotFoundIfUserDoesNotExist()
    {
        // Arrange
        var command = new SetPrimaryEmailCommand(Guid.NewGuid(), "new@parkflow.com");
        var handler = new SetPrimaryEmailCommandHandler(_userAccountRepository, _validator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("User account not found.", result.Message);
    }
}
