using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Auth.Commands.RegisterManualAccount;
using ParkFlow.Application.Features.Auth.DTOs;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingPersonnel;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingStudent;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingVehicle;
using ParkFlow.Application.Features.Vehicles.Command;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Xunit;

namespace Test.Features.Auth;

public class RegistrationValidationTests
{
    [Theory]
    [InlineData("Password123", false)]
    [InlineData("password123!", false)]
    [InlineData("PASSWORD123!", false)]
    [InlineData("Passwordabc!", false)]
    [InlineData("Pass1!", false)]
    [InlineData(" Password123!", false)]
    [InlineData("Password123! ", false)]
    [InlineData("Password123!", true)]
    public void RegistrationEnforcesStrongPasswords(string password, bool valid)
    {
        var result = new RegisterManualAccountValidator().Validate(new RegisterManualAccountCommand("new@example.test", password));
        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void AdminGeneratedPasswordRemainsSupported()
    {
        Assert.True(new RegisterManualAccountValidator().Validate(
            new RegisterManualAccountCommand("new@example.test", IsAdminCreated: true)).IsValid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FullRegistrationRejectsDuplicateIdBeforeCreatingAccount(bool student)
    {
        var users = new FakeUserAccountRepository();
        var identities = new RegRoleAuthIdentityRepository();
        var profiles = new RegRoleUserProfileRepository();
        var students = new RegRoleStudentRepository();
        var personnel = new RegRolePersonnelRepository();
        await students.AddAsync(new Student(Guid.NewGuid(), "2026-00123", "BSIT", "1A", 1));
        await personnel.AddAsync(new Personnel(Guid.NewGuid(), "EMP-00123", "Office"));
        var handler = new RegisterManualAccountHandler(users, identities, profiles, students, personnel,
            new RegRoleGuardRepository(), new FakePasswordHasher(), new FakeJwtService(), new RegisterManualAccountValidator());
        var result = await handler.Handle(new RegisterManualAccountCommand("new@example.test", "Password123!",
            FirstName: "New", LastName: "User", Role: student ? "Student" : "UniversityStaff",
            Student: student ? new RegisterStudentDto(" 2026-00123 ", "BSIT", "1A", 1) : null,
            Personnel: student ? null : new RegisterPersonnelDto(" emp-00123 ", "Office")), default);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Empty(users.Users);
        Assert.Empty(identities.Identities);
        Assert.Empty(profiles.Profiles);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task OnboardingRejectsOtherUsersIdButAllowsOwnId(bool student, bool sameOwner)
    {
        var users = new FakeUserAccountRepository();
        var user = new UserAccount("hash", "09171234567");
        await users.AddAsync(user);
        var profiles = new RegRoleUserProfileRepository();
        var profile = new UserProfile(user.Id, "New", "User", null, null);
        await profiles.AddAsync(profile);
        var students = new RegRoleStudentRepository();
        var personnel = new RegRolePersonnelRepository();
        var ownerId = sameOwner ? profile.Id : Guid.NewGuid();
        if (student) await students.AddAsync(new Student(ownerId, "2023-106763", "BSIT", "1A", 1));
        else await personnel.AddAsync(new Personnel(ownerId, "EMP-00123", "Office"));
        var result = student
            ? await new UpdateOnboardingStudentHandler(profiles, students, personnel, users, new UpdateOnboardingStudentValidator())
                .Handle(new UpdateOnboardingStudentCommand(user.Id, " 2023-106763 ", "BSIT", "1A", 1), default)
            : await new UpdateOnboardingPersonnelHandler(profiles, personnel, students, users, new UpdateOnboardingPersonnelValidator())
                .Handle(new UpdateOnboardingPersonnelCommand(user.Id, " emp-00123 ", "Office"), default);
        Assert.Equal(sameOwner, result.IsSuccess);
        if (!sameOwner) {
            Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
            Assert.Equal(OnboardingStep.Profile, user.OnboardingStep);
        }
    }

    [Theory]
    [InlineData(VehicleType.Motorcycle, true)]
    [InlineData(VehicleType.Car, true)]
    [InlineData(VehicleType.ElectricBike, false)]
    [InlineData((VehicleType)99, false)]
    public void NewVehicleValidatorsOnlyAllowMotorcycleAndCar(VehicleType type, bool valid)
    {
        var userId = Guid.NewGuid();
        Assert.Equal(valid, new CreateVehicleValidator().Validate(
            new CreateVehicleCommand(userId, "ABC1234", "Honda", type)).IsValid);
        Assert.Equal(valid, new UpdateOnboardingVehicleValidator().Validate(
            new UpdateOnboardingVehicleCommand(userId, "ABC1234", "Honda", type)).IsValid);
        Assert.Equal(2, (int)VehicleType.Car);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnboardingRejectsLegacyDuplicateEvenWhenOwnRecordIsFirst(bool student)
    {
        var users = new FakeUserAccountRepository();
        var user = new UserAccount("hash", "09171234567");
        await users.AddAsync(user);
        var profiles = new RegRoleUserProfileRepository();
        var profile = new UserProfile(user.Id, "New", "User", null, null);
        await profiles.AddAsync(profile);
        var students = new RegRoleStudentRepository();
        var personnel = new RegRolePersonnelRepository();
        if (student)
        {
            await students.AddAsync(new Student(profile.Id, "2023106763", "BSIT", "1A", 1));
            await students.AddAsync(new Student(Guid.NewGuid(), "2023-106763", "BSIT", "1A", 1));
        }
        else
        {
            await personnel.AddAsync(new Personnel(profile.Id, "EMP-00123", "Office"));
            await personnel.AddAsync(new Personnel(Guid.NewGuid(), " emp-00123 ", "Office"));
        }
        var result = student
            ? await new UpdateOnboardingStudentHandler(profiles, students, personnel, users, new UpdateOnboardingStudentValidator())
                .Handle(new UpdateOnboardingStudentCommand(user.Id, "2023106763", "BSIT", "1A", 1), default)
            : await new UpdateOnboardingPersonnelHandler(profiles, personnel, students, users, new UpdateOnboardingPersonnelValidator())
                .Handle(new UpdateOnboardingPersonnelCommand(user.Id, "EMP-00123", "Office"), default);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Contains("already registered", result.Message);
        Assert.Equal(OnboardingStep.Profile, user.OnboardingStep);
    }
}
