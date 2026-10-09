using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingStudent;
using ParkFlow.Application.Features.Onboarding.Queries.CheckRegistrationId;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.Auth;
using Xunit;

namespace Test.Features.Onboarding;

public class RegistrationIdAvailabilityTests
{
    [Theory]
    [InlineData("Student", false, false)]
    [InlineData("Student", true, false)]
    [InlineData("Student", true, true)]
    [InlineData("UniversityStaff", false, false)]
    [InlineData("UniversityStaff", true, false)]
    [InlineData("UniversityStaff", true, true)]
    [InlineData("NonAcademicPersonnel", true, false)]
    public async Task AvailabilityUsesAllSavedIdsAndOnlyAllowsTheCurrentOwnerToResume(string role, bool registered, bool sameOwner)
    {
        var userId = Guid.NewGuid();
        var profiles = new RegRoleUserProfileRepository();
        var profile = new UserProfile(userId, "New", "User", null, null);
        await profiles.AddAsync(profile);
        var students = new RegRoleStudentRepository();
        var personnel = new RegRolePersonnelRepository();
        var student = role == "Student";
        if (registered)
        {
            // No related account/profile needs to be materialized to detect a stored ID.
            var owner = sameOwner ? profile.Id : Guid.NewGuid();
            if (student) await students.AddAsync(new Student(owner, "2023106763", "BSIT", "1A", 1));
            else await personnel.AddAsync(new Personnel(owner, "EMP-123", "Office"));
        }
        var handler = new CheckRegistrationIdHandler(profiles, students, personnel);
        var result = await handler.Handle(new CheckRegistrationIdQuery(userId, student ? "2023106763" : "emp-123", role), default);
        var available = !registered || sameOwner;
        Assert.Equal(available, result.IsSuccess);
        Assert.Equal(available, result.Data);
        if (!available)
        {
            Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
            Assert.Contains("already registered", result.Message);
        }
    }

    [Theory]
    [InlineData("2023106763", true)]
    [InlineData("202310676", false)]
    [InlineData("20231067630", false)]
    [InlineData("2023A06763", false)]
    [InlineData("２０２３１０６７６３", false)]
    [InlineData("2023-106763", false)]
    [InlineData("", false)]
    public async Task StudentAvailabilityRequiresTheTenDigitInputFormat(string number, bool valid)
    {
        var handler = new CheckRegistrationIdHandler(new RegRoleUserProfileRepository(), new RegRoleStudentRepository(), new RegRolePersonnelRepository());
        var result = await handler.Handle(new CheckRegistrationIdQuery(Guid.NewGuid(), number, "Student"), default);
        Assert.Equal(valid, result.IsSuccess);
        if (!valid) Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
    }

    [Fact]
    public async Task UnauthenticatedCheckCannotReportAnIdAsAvailable()
    {
        var result = await new CheckRegistrationIdHandler(new RegRoleUserProfileRepository(), new RegRoleStudentRepository(), new RegRolePersonnelRepository())
            .Handle(new CheckRegistrationIdQuery(Guid.Empty, "2023106763", "Student"), default);
        Assert.False(result.IsSuccess);
        Assert.False(result.Data);
        Assert.Equal(ErrorCode.Unauthorized, result.ErrorCode);
    }

    [Fact]
    public async Task AnotherAccountCannotSaveTheExactReportedStudentNumber()
    {
        var users = new FakeUserAccountRepository();
        var user = new UserAccount("hash", "09171234567");
        await users.AddAsync(user);
        var profiles = new RegRoleUserProfileRepository();
        var profile = new UserProfile(user.Id, "New", "User", null, null);
        await profiles.AddAsync(profile);
        var students = new RegRoleStudentRepository();
        await students.AddAsync(new Student(Guid.NewGuid(), "2023106763", "BSIT", "1A", 1));
        var result = await new UpdateOnboardingStudentHandler(profiles, students, new RegRolePersonnelRepository(), users,
            new UpdateOnboardingStudentValidator()).Handle(new UpdateOnboardingStudentCommand(user.Id, "2023106763", "BSIT", "1A", 1), default);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Single(students.Students);
        Assert.Equal(OnboardingStep.Profile, user.OnboardingStep);
    }

    [Theory]
    [InlineData("2023106763", true)]
    [InlineData("2023-106763", true)]
    [InlineData("202310676", false)]
    [InlineData("20231067630", false)]
    [InlineData("2023A06763", false)]
    public void StudentSaveCannotBypassTheTenDigitRule(string number, bool valid)
    {
        Assert.Equal(valid, new UpdateOnboardingStudentValidator()
            .Validate(new UpdateOnboardingStudentCommand(Guid.NewGuid(), number, "BSIT", "1A", 1)).IsValid);
    }
}
