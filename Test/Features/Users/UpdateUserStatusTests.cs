using System.Reflection;
using ParkFlow.Application.Features.Users.Commands.UpdateUserStatus;
using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Verification;
using Xunit;

namespace Test.Features.Users;

public class UpdateUserStatusTests
{
    [Theory]
    [InlineData(true, true, true, false, AccountStatus.Active)]
    [InlineData(false, true, true, false, AccountStatus.PendingVerification)]
    [InlineData(true, false, true, false, AccountStatus.PendingVerification)]
    [InlineData(true, true, false, false, AccountStatus.PendingVerification)]
    [InlineData(true, true, true, true, AccountStatus.PendingVerification)]
    public void StudentStatusRequiresAllExistingApprovals(bool vehicleApproved, bool driverApproved,
        bool emailVerified, bool duplicateIdentifier, AccountStatus expected)
    {
        var subject = VerificationRulesTests.Subject(VerificationType.Account) with
        { EmailVerified = emailVerified, DuplicateIdentifier = duplicateIdentifier };
        Assert.Equal(expected, VerificationService.StudentAccountStatus(subject, VerificationRulesTests.Policy(), vehicleApproved, driverApproved));
    }

    [Theory]
    [InlineData(AccountStatus.Active)]
    [InlineData(AccountStatus.PendingVerification)]
    public async Task SuspendedStudentReturnsPersistedStatusInsteadOfRequestedActive(AccountStatus restored)
    {
        var user = StudentAccount(AccountStatus.Suspended);
        var repository = new FakeUserAccountRepository(); repository.Users.Add(user);
        var store = DispatchProxy.Create<IVerificationStore, RestoreStore>();
        ((RestoreStore)(object)store).Restore = id =>
        {
            Assert.Equal(user.Id, id); user.UpdateStatus(restored); return Task.FromResult<AccountStatus?>(restored);
        };
        var result = await new UpdateUserStatusHandler(repository, store, null!).Handle(new(user.Id, "Active"), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Data!.Id);
        Assert.Equal(restored.ToString(), result.Data.Status);
        Assert.Equal(restored, (await repository.GetByIdAsync(user.Id))!.Status);
    }

    [Fact]
    public async Task PendingStudentCannotUseActivationToBypassVerification()
    {
        var user = StudentAccount(AccountStatus.PendingVerification);
        var repository = new FakeUserAccountRepository(); repository.Users.Add(user);
        var result = await new UpdateUserStatusHandler(repository, null!, null!).Handle(new(user.Id, "Active"), default);
        Assert.False(result.IsSuccess); Assert.Equal(AccountStatus.PendingVerification, user.Status);
    }

    [Fact]
    public async Task FailedRestorationDoesNotReturnSuccessOrMutateStatus()
    {
        var user = StudentAccount(AccountStatus.Suspended);
        var repository = new FakeUserAccountRepository(); repository.Users.Add(user);
        var store = DispatchProxy.Create<IVerificationStore, RestoreStore>();
        ((RestoreStore)(object)store).Restore = _ => throw new IOException("Database unavailable");
        await Assert.ThrowsAsync<IOException>(() => new UpdateUserStatusHandler(repository, store, null!).Handle(new(user.Id, "Active"), default));
        Assert.Equal(AccountStatus.Suspended, user.Status);
    }

    private static UserAccount StudentAccount(AccountStatus status)
    {
        var user = new UserAccount("hash", null); user.UpdateStatus(status);
        var profile = new UserProfile(user.Id, "Juan", "Dela Cruz", null, null);
        profile.Student = new Student(profile.Id, "2023106763", "BSCS", "4A", 4);
        user.UserProfile = profile; return user;
    }

    public class RestoreStore : DispatchProxy
    {
        public Func<Guid, Task<AccountStatus?>> Restore { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method?.Name == nameof(IVerificationStore.RestoreAccountAsync) ? Restore((Guid)args![0]!)
                : throw new InvalidOperationException("Unexpected verification operation: " + method?.Name);
    }
}
