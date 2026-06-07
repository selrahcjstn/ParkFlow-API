using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.DTOs;
using ParkFlow.Application.Features.Users.Queries.GetUserCredentials;
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

public class FakeUserAccountRepository : IUserAccountRepository
{
    public List<UserAccount> Users { get; } = new();

    public Task<UserAccount?> GetByEmailAsync(string email) => Task.FromResult(Users.FirstOrDefault(u => u.AuthIdentities.Any(i => i.Email == email)));
    public Task<UserAccount?> GetByAuthProviderExternalIdAsync(AuthProvider authProvider, string externalProviderId) =>
        Task.FromResult(Users.FirstOrDefault(u => u.AuthProvider == authProvider && u.ExternalProviderId == externalProviderId));
    public Task<UserAccount?> GetByIdAsync(Guid id) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
    public Task<UserAccount?> GetByPhoneNumberAsync(string phoneNumber) => Task.FromResult(Users.FirstOrDefault(u => u.PhoneNumber == phoneNumber));
    public Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null)
    {
        var q = Users.Where(u => u.AuthIdentities.Any(i => i.Email == email));
        if (excludeUserId.HasValue) q = q.Where(u => u.Id != excludeUserId.Value);
        return Task.FromResult(q.Any());
    }
    public Task AddAsync(UserAccount user) { Users.Add(user); return Task.CompletedTask; }
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

public class GetUserCredentialsHandlerTests
{
    private readonly FakeUserAccountRepository _userAccountRepository;

    public GetUserCredentialsHandlerTests()
    {
        _userAccountRepository = new FakeUserAccountRepository();
    }

    [Fact]
    public async Task GetUserCredentialsHandler_ShouldSuccessfullyRetrieveCredentialsAndLinkedIdentities()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "john.doe@test.com";
        var user = new UserAccount("passhash", "+639123456789");
        
        // Force the ID since BaseEntity generates Guid
        var idField = typeof(BaseEntity).GetProperty("Id");
        idField?.SetValue(user, userId);

        // Add linked auth identities
        user.AuthIdentities.Add(new AuthIdentity(userId, AuthProvider.Manual, email, null, "passhash", true, true));
        user.AuthIdentities.Add(new AuthIdentity(userId, AuthProvider.Microsoft, email, "ms-oauth-id-123", null, true, false));

        await _userAccountRepository.AddAsync(user);

        var query = new GetUserCredentialsQuery(userId);
        var handler = new GetUserCredentialsHandler(_userAccountRepository);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(email, result.Data.Email);
        Assert.Equal(AuthProvider.Manual.ToString(), result.Data.PrimaryProvider);
        Assert.Equal(2, result.Data.LinkedIdentities.Count);

        var manualIdentity = result.Data.LinkedIdentities.First(i => i.Provider == AuthProvider.Manual.ToString());
        Assert.Equal(email, manualIdentity.Email);
        Assert.True(manualIdentity.IsVerified);
        Assert.True(manualIdentity.IsPrimary);

        var microsoftIdentity = result.Data.LinkedIdentities.First(i => i.Provider == AuthProvider.Microsoft.ToString());
        Assert.Equal(email, microsoftIdentity.Email);
        Assert.Equal("ms-oauth-id-123", microsoftIdentity.ProviderId);
        Assert.True(microsoftIdentity.IsVerified);
        Assert.False(microsoftIdentity.IsPrimary);
    }

    [Fact]
    public async Task GetUserCredentialsHandler_ShouldReturnNotFoundIfUserDoesNotExist()
    {
        // Arrange
        var query = new GetUserCredentialsQuery(Guid.NewGuid());
        var handler = new GetUserCredentialsHandler(_userAccountRepository);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.ErrorCode);
    }
}
