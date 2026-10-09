using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingCor;
using ParkFlow.Application.Features.Users.Commands.UpdateUserAccountAdmin;
using ParkFlow.Application.Features.Users.DTOs;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using ParkFlow.Persistence;
using ParkFlow.Persistence.Repositories;
using Test.Features.Auth;
using Test.Features.Files;
using Xunit;

namespace Test.Features.Onboarding;

public class DuplicateRegistrationIdTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task DocumentSubmissionRechecksOwnershipBeforeSaving(bool student, bool duplicate)
    {
        var users = new FakeUserAccountRepository();
        var user = new UserAccount("hash", "09171234567");
        user.UpdateOnboardingStep(OnboardingStep.Schedule);
        await users.AddAsync(user);
        var profiles = new RegRoleUserProfileRepository();
        var profile = new UserProfile(user.Id, "Juan", "Cruz", null, null);
        await profiles.AddAsync(profile);
        var students = new RegRoleStudentRepository();
        var personnel = new RegRolePersonnelRepository();
        if (student)
        {
            await students.AddAsync(new Student(profile.Id, "202600123", "BSIT", "1A", 1));
            if (duplicate) await students.AddAsync(new Student(Guid.NewGuid(), "2026-00123", "BSIT", "1A", 1));
        }
        else
        {
            await personnel.AddAsync(new Personnel(profile.Id, "EMP-123", "Office"));
            if (duplicate) await personnel.AddAsync(new Personnel(Guid.NewGuid(), " emp-123 ", "Office"));
        }
        var submissions = new InMemoryCorSubmissionRepository();
        var handler = new UpdateOnboardingCorHandler(submissions, users, new UpdateOnboardingCorValidator(), profiles, students, personnel);
        var result = await handler.Handle(new UpdateOnboardingCorCommand(user.Id, "2026-2027",
            "https://example.test/cor.pdf", "https://example.test/orcr.pdf", "https://example.test/motor.jpg"), default);
        Assert.True(result.IsSuccess == !duplicate, result.Message);
        if (duplicate)
        {
            Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
            Assert.Contains("already registered", result.Message);
            Assert.Empty(submissions.Submissions);
            Assert.Equal(OnboardingStep.Schedule, user.OnboardingStep);
        }
        else
        {
            Assert.Single(submissions.Submissions);
            Assert.Equal(OnboardingStep.Done, user.OnboardingStep);
        }
    }

    [Fact]
    public async Task DocumentsCannotSkipRoleIdStep()
    {
        var users = new FakeUserAccountRepository();
        var user = new UserAccount("hash", "09171234567");
        await users.AddAsync(user);
        var profiles = new RegRoleUserProfileRepository();
        await profiles.AddAsync(new UserProfile(user.Id, "Juan", "Cruz", null, null));
        var submissions = new InMemoryCorSubmissionRepository();
        var handler = new UpdateOnboardingCorHandler(submissions, users, new UpdateOnboardingCorValidator(), profiles,
            new RegRoleStudentRepository(), new RegRolePersonnelRepository());
        var result = await handler.Handle(new UpdateOnboardingCorCommand(user.Id, "2026-2027", "https://example.test/cor.pdf", null, null), default);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BadRequest, result.ErrorCode);
        Assert.Contains("ID number", result.Message);
        Assert.Empty(submissions.Submissions);
        Assert.Equal(OnboardingStep.Profile, user.OnboardingStep);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AdminCannotReplaceAnIdWithAnotherAccountsId(bool student)
    {
        var users = new FakeUserAccountRepository();
        var user = new UserAccount("hash", "09171234567");
        await users.AddAsync(user);
        var profiles = new RegRoleUserProfileRepository();
        var profile = new UserProfile(user.Id, "Original", "Name", null, null);
        user.UserProfile = profile;
        await profiles.AddAsync(profile);
        var students = new RegRoleStudentRepository();
        var personnel = new RegRolePersonnelRepository();
        if (student)
        {
            profile.Student = new Student(profile.Id, "202600124", "BSIT", "1A", 1);
            await students.AddAsync(profile.Student);
            await students.AddAsync(new Student(Guid.NewGuid(), "2026-00123", "BSIT", "1A", 1));
        }
        else
        {
            profile.Personnel = new Personnel(profile.Id, "EMP-124", "Office");
            await personnel.AddAsync(profile.Personnel);
            await personnel.AddAsync(new Personnel(Guid.NewGuid(), "EMP-123", "Office"));
        }
        var handler = new UpdateUserAccountAdminHandler(users, profiles, students, personnel,
            new RegRoleGuardRepository(), new Test.Features.Users.FakeAdminRepository(), new FakePasswordHasher());
        var request = new UpdateUserAccountAdminRequest("Changed", "Name", null, null, "09999999999",
            student ? "Student" : "UniversityStaff", "Active", null, null,
            student ? new UpdateUserStudentRequest(" 2026-00123 ", "BSIT", "1A", 1) : null,
            student ? null : new UpdateUserPersonnelRequest(" emp-123 ", "Office"), null);
        var result = await handler.Handle(new UpdateUserAccountAdminCommand(user.Id, request), default);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, result.ErrorCode);
        Assert.Contains("already registered", result.Message);
        Assert.Equal("Original", profile.FirstName);
        Assert.Equal("09171234567", user.PhoneNumber);
        Assert.Equal(AccountStatus.PendingVerification, user.Status);
        if (student) Assert.Equal("202600124", profile.Student!.StudentNumber);
        else Assert.Equal("EMP-124", profile.Personnel!.IdCardNumber);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task DatabaseIdConflictHasAReadableErrorOnInsertAndUpdate(bool student, bool update)
    {
        using var context = new ConstraintFailureContext(student ? "IX_Students_StudentNumber" : "IX_Personnel_IdCardNumber");
        var conflict = await Assert.ThrowsAsync<RegistrationIdConflictException>(async () =>
        {
            if (student)
            {
                var repository = new StudentRepository(context);
                var record = new Student(Guid.NewGuid(), "202600123", "BSIT", "1A", 1);
                if (update) await repository.UpdateAsync(record);
                else await repository.AddAsync(record);
            }
            else
            {
                var repository = new PersonnelRepository(context);
                var record = new Personnel(Guid.NewGuid(), "EMP-123", "Office");
                if (update) await repository.UpdateAsync(record);
                else await repository.AddAsync(record);
            }
        });
        Assert.Contains(student ? "student ID number" : "employee ID number", conflict.Message);
        Assert.Contains("already registered", conflict.Message);
        Assert.DoesNotContain("23505", conflict.Message);
    }

    [Fact]
    public async Task UnrelatedDatabaseErrorsAreNotMisreportedAsDuplicateIds()
    {
        using var context = new ConstraintFailureContext("PK_Students");
        await Assert.ThrowsAsync<DbUpdateException>(() => new StudentRepository(context)
            .AddAsync(new Student(Guid.NewGuid(), "202600123", "BSIT", "1A", 1)));
    }

    [Fact]
    public void StudentAndEmployeeIdsStillHaveDatabaseUniqueConstraints()
    {
        using var context = new ConstraintFailureContext("unused");
        Assert.Contains(context.Model.FindEntityType(typeof(Student))!.GetIndexes(),
            index => index.IsUnique && index.Properties.Single().Name == nameof(Student.StudentNumber));
        Assert.Contains(context.Model.FindEntityType(typeof(Personnel))!.GetIndexes(),
            index => index.IsUnique && index.Properties.Single().Name == nameof(Personnel.IdCardNumber));
    }

    // Simulates the unique-index rejection without connecting to a real database.
    private sealed class ConstraintFailureContext(string constraint) : AppDbContext(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Database constraint failure", new PostgresException(
                "duplicate value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: constraint));
    }
}
