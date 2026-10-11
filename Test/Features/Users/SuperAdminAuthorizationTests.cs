using Microsoft.Extensions.Configuration;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.RegisterAdmin.Commands.CreateAdminAccount;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Xunit;
using AccountRepository = Test.Features.Auth.FakeUserAccountRepository;
using IdentityRepository = Test.Features.Auth.FakeAuthIdentityRepository;
using Hasher = Test.Features.Auth.FakePasswordHasher;

namespace Test.Features.Users;

public class SuperAdminAuthorizationTests
{
    [Theory]
    [InlineData("superadmin@parkflow.com")]
    [InlineData("superadmin6767@gmail.com")]
    [InlineData("superadmin-other@example.com")]
    public async Task EmailAloneDoesNotGrantSuperAdminPrivileges(string email)
    {
        var accounts = new AccountRepository();
        var admins = new FakeAdminRepository();
        var user = new UserAccount("existing-hash", "+639171234567");
        user.Verify();
        user.AuthIdentities.Add(AuthIdentity.CreateManual(user.Id, email, "existing-hash", true));
        user.UserProfile = new UserProfile(user.Id, "Existing", "User", null, null);
        accounts.Users.Add(user);
        var handler = CreateHandler(accounts, admins);

        var result = await handler.Handle(Command(user.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Forbidden, result.ErrorCode);
        Assert.Single(accounts.Users);
        Assert.Empty(admins.Admins);
    }

    [Theory]
    [InlineData("superadmin6767@gmail.com")]
    [InlineData("authorized@example.com")]
    public async Task StoredSuperAdminRoleAuthorizesRegardlessOfEmail(string email)
    {
        var accounts = new AccountRepository();
        var admins = new FakeAdminRepository();
        var user = new UserAccount("existing-hash", "+639171234567");
        user.Verify();
        user.AuthIdentities.Add(AuthIdentity.CreateManual(user.Id, email, "existing-hash", true));
        user.UserProfile = new UserProfile(user.Id, "Super", "Admin", null, null);
        var admin = new Admin(user.UserProfile, RoleLevel.SuperAdmin);
        user.UserProfile.Admin = admin;
        admins.Admins.Add(admin);
        accounts.Users.Add(user);

        var result = await CreateHandler(accounts, admins).Handle(Command(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, accounts.Users.Count);
        Assert.Equal(2, admins.Admins.Count);
    }

    private static CreateAdminAccountHandler CreateHandler(AccountRepository accounts, FakeAdminRepository admins) =>
        new(accounts, new IdentityRepository(), new FakeUserProfileRepository(), admins, new Hasher(),
            new CreateAdminAccountValidator(), new ConfigurationBuilder().Build());

    private static CreateAdminAccountCommand Command(Guid userId) => new(
        new AccountDto("new-admin@example.com", "AdminTest123!", "+639171234568"),
        new ProfileDto("New", "Admin", null, null), RoleLevel.Admin, CurrentUserId: userId);
}
