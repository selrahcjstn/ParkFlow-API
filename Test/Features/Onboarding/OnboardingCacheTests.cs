using Microsoft.Extensions.Caching.Memory;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingCor;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingProfile;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingStudent;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingPersonnel;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingVehicle;
using ParkFlow.Application.Features.Profiles.Queries;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Infrastructure.Caching;
using Test.Features.Auth;
using Test.Features.Files;
using Xunit;

namespace Test.Features.Onboarding;

public class OnboardingCacheTests
{
    [Theory]
    [InlineData("profile")]
    [InlineData("student")]
    [InlineData("faculty")]
    [InlineData("staff")]
    [InlineData("vehicle")]
    public async Task EachSavedStepInvalidatesOnlyTheAffectedAccount(string step)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memory);
        var users = new FakeUserAccountRepository();
        var user = new UserAccount("hash", "09171234567");
        await users.AddAsync(user);
        var profiles = new RegRoleUserProfileRepository();
        var profile = new UserProfile(user.Id, "Juan", "Cruz", null, null) { UserAccount = user };
        await profiles.AddAsync(profile);
        var students = new RegRoleStudentRepository();
        var personnel = new RegRolePersonnelRepository();
        var otherId = Guid.NewGuid();
        await cache.SetAsync(CacheKeys.UserProfile(user.Id), "stale profile");
        await cache.SetAsync(CacheKeys.UserVehicles(user.Id), "stale vehicles");
        await cache.SetAsync(CacheKeys.UserProfile(otherId), "other profile");
        await cache.SetAsync(CacheKeys.UserVehicles(otherId), "other vehicles");

        var result = step switch
        {
            "profile" => await new UpdateOnboardingProfileHandler(users, profiles, new UpdateOnboardingProfileValidator(), cache)
                .Handle(new UpdateOnboardingProfileCommand(user.Id, "09171234567", "Juan", "Cruz", null, null), default),
            "student" => await new UpdateOnboardingStudentHandler(profiles, students, personnel, users, new UpdateOnboardingStudentValidator(), cache)
                .Handle(new UpdateOnboardingStudentCommand(user.Id, "202600123", "BSIT", "1A", 1), default),
            "faculty" or "staff" => await new UpdateOnboardingPersonnelHandler(profiles, personnel, students, users, new UpdateOnboardingPersonnelValidator(), cache)
                .Handle(new UpdateOnboardingPersonnelCommand(user.Id, "EMP-123", "Office", step == "staff" ? "NonAcademicPersonnel" : "UniversityStaff"), default),
            _ => await new UpdateOnboardingVehicleHandler(new Test.Features.Violations.FakeVehicleRepository(), users,
                new Test.Features.Vehicles.FakeQrCodeService(), new UpdateOnboardingVehicleValidator(), cache)
                .Handle(new UpdateOnboardingVehicleCommand(user.Id, "ABC1234", "Honda", VehicleType.Motorcycle), default)
        };

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(await cache.GetAsync<string>(CacheKeys.UserProfile(user.Id)));
        if (step == "vehicle") Assert.Null(await cache.GetAsync<string>(CacheKeys.UserVehicles(user.Id)));
        Assert.Equal("other profile", await cache.GetAsync<string>(CacheKeys.UserProfile(otherId)));
        Assert.Equal("other vehicles", await cache.GetAsync<string>(CacheKeys.UserVehicles(otherId)));
    }

    [Fact]
    public async Task DuplicateIdRejectionDoesNotInvalidateOrChangeProgress()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memory);
        var users = new FakeUserAccountRepository();
        var user = new UserAccount("hash", "09171234567");
        await users.AddAsync(user);
        var profiles = new RegRoleUserProfileRepository();
        var profile = new UserProfile(user.Id, "Juan", "Cruz", null, null) { UserAccount = user };
        await profiles.AddAsync(profile);
        var students = new RegRoleStudentRepository();
        await students.AddAsync(new Student(Guid.NewGuid(), "202600123", "BSIT", "1A", 1));
        await cache.SetAsync(CacheKeys.UserProfile(user.Id), "unchanged profile");
        var result = await new UpdateOnboardingStudentHandler(profiles, students, new RegRolePersonnelRepository(), users,
            new UpdateOnboardingStudentValidator(), cache).Handle(new UpdateOnboardingStudentCommand(user.Id, "202600123", "BSIT", "1A", 1), default);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Equal(OnboardingStep.Profile, user.OnboardingStep);
        Assert.Equal("unchanged profile", await cache.GetAsync<string>(CacheKeys.UserProfile(user.Id)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DocumentCompletionDoesNotReturnCachedMissingRole(bool student)
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memory);
        var users = new FakeUserAccountRepository();
        var user = new UserAccount("hash", "09171234567");
        user.UpdateOnboardingStep(OnboardingStep.Student);
        await users.AddAsync(user);
        var profiles = new RegRoleUserProfileRepository();
        var profile = new UserProfile(user.Id, "Juan", "Cruz", null, null) { UserAccount = user };
        await profiles.AddAsync(profile);
        var submissions = new InMemoryCorSubmissionRepository();
        var query = new GetMyProfileHandler(profiles, new FakeUserContext { UserId = user.Id }, submissions, cacheService: cache);

        // The app reads the profile before the role is saved and caches this snapshot.
        var before = await query.Handle(new GetMyProfileQuery(), default);
        Assert.Null(before.Data!.StudentNumber);
        Assert.Null(before.Data.EmployeeIdNumber);

        if (student) profile.Student = new Student(profile.Id, "202600123", "BSIT", "1A", 1);
        else profile.Personnel = new Personnel(profile.Id, "EMP-123", "Office");
        await cache.SetAsync(CacheKeys.UserVehicles(user.Id), "stale document links");
        var students = new RegRoleStudentRepository();
        var personnel = new RegRolePersonnelRepository();
        if (profile.Student != null) await students.AddAsync(profile.Student);
        if (profile.Personnel != null) await personnel.AddAsync(profile.Personnel);
        var handler = new UpdateOnboardingCorHandler(submissions, users, new UpdateOnboardingCorValidator(), profiles, students, personnel, cacheService: cache);
        var submitted = await handler.Handle(new UpdateOnboardingCorCommand(user.Id, "2026-2027",
            "https://example.test/cor.pdf", "https://example.test/orcr.pdf", "https://example.test/motor.jpg"), default);
        Assert.True(submitted.IsSuccess);
        Assert.Equal(OnboardingStep.Done, user.OnboardingStep);
        Assert.Null(await cache.GetAsync<string>(CacheKeys.UserVehicles(user.Id)));

        var after = await query.Handle(new GetMyProfileQuery(), default);
        Assert.Equal(OnboardingStep.Done, after.Data!.OnboardingStep);
        Assert.Equal(CorVerificationStatus.Pending, after.Data.CorVerificationStatus);
        Assert.Equal(student ? "202600123" : null, after.Data.StudentNumber);
        Assert.Equal(student ? null : "EMP-123", after.Data.EmployeeIdNumber);
    }
}
