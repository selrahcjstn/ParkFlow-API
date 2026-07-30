using ParkFlow.Application.Features.RegisterAdmin.Commands.CreateAdminAccount;
using ParkFlow.Application.Features.RegisterGuard.Commands.CreateGuardAccount;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Test.Features.Auth;

public class FakeTestConfiguration : IConfiguration
{
    private readonly Dictionary<string, string?> _values;
    public FakeTestConfiguration(Dictionary<string, string?> values) => _values = values;
    public string? this[string key]
    {
        get => _values.TryGetValue(key, out var val) ? val : null;
        set => _values[key] = value;
    }
    public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
    public IChangeToken GetReloadToken() => null!;
    public IConfigurationSection GetSection(string key) => null!;
}

public class RegRoleAuthIdentityRepository : IAuthIdentityRepository
{
    public List<AuthIdentity> Identities { get; } = new();

    public Task AddAsync(AuthIdentity identity)
    {
        Identities.Add(identity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AuthIdentity identity) => Task.CompletedTask;
    public Task DeleteAsync(AuthIdentity identity) => Task.CompletedTask;
    public Task<AuthIdentity?> GetByEmailAsync(string email) =>
        Task.FromResult(Identities.FirstOrDefault(i => i.Email != null && i.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
    public Task<AuthIdentity?> GetByProviderIdAsync(AuthProvider provider, string providerId) =>
        Task.FromResult(Identities.FirstOrDefault(i => i.Provider == provider && i.ProviderId == providerId));
    public Task<IEnumerable<AuthIdentity>> GetByAccountIdAsync(Guid accountId) =>
        Task.FromResult<IEnumerable<AuthIdentity>>(Identities.Where(i => i.UserAccountId == accountId));
}

public class RegRoleUserProfileRepository : IUserProfileRepository
{
    public List<UserProfile> Profiles { get; } = new();

    public Task AddAsync(UserProfile profile)
    {
        Profiles.Add(profile);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(UserProfile profile) => Task.CompletedTask;
    public Task<UserProfile?> GetByUserIdAsync(Guid userId) =>
        Task.FromResult(Profiles.FirstOrDefault(p => p.UserAccountId == userId));
    public Task<UserProfile?> GetByIdAsync(Guid id) =>
        Task.FromResult(Profiles.FirstOrDefault(p => p.Id == id));
}

public class RegRoleAdminRepository : IAdminRepository
{
    public List<Admin> Admins { get; } = new();

    public Task AddAsync(Admin admin)
    {
        Admins.Add(admin);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Admin admin) => Task.CompletedTask;
    public Task<Admin?> GetByUserProfileIdAsync(Guid userProfileId) =>
        Task.FromResult(Admins.FirstOrDefault(a => a.UserProfileId == userProfileId));
    public Task<IEnumerable<Admin>> ListAllAsync() => Task.FromResult<IEnumerable<Admin>>(Admins);
}

public class RegRoleGuardRepository : IGuardRepository
{
    public List<Guard> Guards { get; } = new();

    public Task AddAsync(Guard guard)
    {
        Guards.Add(guard);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Guard guard) => Task.CompletedTask;
    public Task<Guard?> GetByUserProfileIdAsync(Guid userProfileId) =>
        Task.FromResult(Guards.FirstOrDefault(g => g.UserProfileId == userProfileId));
    public Task<IEnumerable<Guard>> ListAllAsync() => Task.FromResult<IEnumerable<Guard>>(Guards);
}

public class RegistrationRoleTests
{
    [Fact]
    public async Task CreateAdminAccountHandler_ShouldSuccessfullyCreateActiveAdminAccount()
    {
        // Arrange
        var userRepo = new FakeUserAccountRepository();
        var authRepo = new RegRoleAuthIdentityRepository();
        var profileRepo = new RegRoleUserProfileRepository();
        var adminRepo = new RegRoleAdminRepository();
        var passwordHasher = new FakePasswordHasher();
        var validator = new CreateAdminAccountValidator();

        var config = new FakeTestConfiguration(new Dictionary<string, string?>
        {
            { "AdminSettings:RegistrationKey", "ParkFlowSecretBootstrapAdminKey2026" }
        });

        var handler = new CreateAdminAccountHandler(
            userRepo,
            authRepo,
            profileRepo,
            adminRepo,
            passwordHasher,
            validator,
            config
        );

        var command = new CreateAdminAccountCommand(
            new ParkFlow.Application.Features.RegisterAdmin.Commands.CreateAdminAccount.AccountDto("admin@parkflow.app", "AdminPass123!", "09171234567"),
            new ParkFlow.Application.Features.RegisterAdmin.Commands.CreateAdminAccount.ProfileDto("System", "Admin", null, null),
            RoleLevel.Admin,
            RegistrationKey: "ParkFlowSecretBootstrapAdminKey2026"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Data);

        var createdUser = await userRepo.GetByIdAsync(result.Data);
        Assert.NotNull(createdUser);
        Assert.Equal(AccountStatus.Active, createdUser.Status);
        Assert.Equal(OnboardingStep.Done, createdUser.OnboardingStep);

        var createdProfile = await profileRepo.GetByUserIdAsync(result.Data);
        Assert.NotNull(createdProfile);
        Assert.Equal("System", createdProfile.FirstName);

        var createdAdmin = await adminRepo.GetByUserProfileIdAsync(createdProfile.Id);
        Assert.NotNull(createdAdmin);
        Assert.Equal(RoleLevel.Admin, createdAdmin.RoleLevel);
    }

    [Fact]
    public async Task CreateGuardAccountHandler_ShouldSuccessfullyCreateActiveGuardAccount()
    {
        // Arrange
        var userRepo = new FakeUserAccountRepository();
        var authRepo = new RegRoleAuthIdentityRepository();
        var profileRepo = new RegRoleUserProfileRepository();
        var guardRepo = new RegRoleGuardRepository();
        var passwordHasher = new FakePasswordHasher();
        var validator = new CreateGuardAccountValidator();

        var handler = new CreateGuardAccountHandler(
            userRepo,
            authRepo,
            profileRepo,
            guardRepo,
            passwordHasher,
            validator
        );

        var command = new CreateGuardAccountCommand(
            new ParkFlow.Application.Features.RegisterGuard.Commands.CreateGuardAccount.AccountDto("guard1@parkflow.app", "GuardPass123!", "09179876543"),
            new ParkFlow.Application.Features.RegisterGuard.Commands.CreateGuardAccount.ProfileDto("Gate", "Guard", null, null),
            1
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Data);

        var createdUser = await userRepo.GetByIdAsync(result.Data);
        Assert.NotNull(createdUser);
        Assert.Equal(AccountStatus.Active, createdUser.Status);
        Assert.Equal(OnboardingStep.Done, createdUser.OnboardingStep);

        var createdProfile = await profileRepo.GetByUserIdAsync(result.Data);
        Assert.NotNull(createdProfile);

        var createdGuard = await guardRepo.GetByUserProfileIdAsync(createdProfile.Id);
        Assert.NotNull(createdGuard);
        Assert.Equal(1, createdGuard.AssignedGate);
    }
}
