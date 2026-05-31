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
    public async Task SetPrimaryEmailHandler_ShouldSuccessfullyUpdatePrimaryAndManualEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldEmail = "old@parkflow.com";
        var newEmail = "new@parkflow.com";
        var user = new UserAccount(oldEmail, "hash", "+639000000000");

        var idProperty = typeof(BaseEntity).GetProperty("Id");
        idProperty?.SetValue(user, userId);

        var manualIdentity = AuthIdentity.CreateManual(userId, oldEmail, "hash");
        await _authIdentityRepository.AddAsync(manualIdentity);
        await _userAccountRepository.AddAsync(user);

        var command = new SetPrimaryEmailCommand(userId, newEmail);
        var handler = new SetPrimaryEmailCommandHandler(_userAccountRepository, _authIdentityRepository, _validator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);

        var updatedUser = Assert.Single(_userAccountRepository.Users);
        Assert.Equal(newEmail, updatedUser.Email);

        var updatedIdentity = Assert.Single(_authIdentityRepository.Identities);
        Assert.Equal(newEmail, updatedIdentity.Email);
    }

    [Fact]
    public async Task SetPrimaryEmailHandler_ShouldReturnConflictIfEmailIsAlreadyTaken()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var email1 = "user1@parkflow.com";
        var email2 = "user2@parkflow.com";

        var user1 = new UserAccount(email1, "hash", "+639000000000");
        var user2 = new UserAccount(email2, "hash", "+639000000001");

        var idProperty = typeof(BaseEntity).GetProperty("Id");
        idProperty?.SetValue(user1, userId1);
        idProperty?.SetValue(user2, userId2);

        await _userAccountRepository.AddAsync(user1);
        await _userAccountRepository.AddAsync(user2);

        // Try to update user2's email to user1's email (taken)
        var command = new SetPrimaryEmailCommand(userId2, email1);
        var handler = new SetPrimaryEmailCommandHandler(_userAccountRepository, _authIdentityRepository, _validator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal("Email is already taken by another account.", result.Message);
    }

    [Fact]
    public async Task SetPrimaryEmailHandler_ShouldReturnNotFoundIfUserDoesNotExist()
    {
        // Arrange
        var command = new SetPrimaryEmailCommand(Guid.NewGuid(), "new@parkflow.com");
        var handler = new SetPrimaryEmailCommandHandler(_userAccountRepository, _authIdentityRepository, _validator);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("User account not found.", result.Message);
    }
}
