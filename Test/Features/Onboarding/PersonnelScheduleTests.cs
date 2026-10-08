using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingSchedule;
using ParkFlow.Application.Features.Schedules.Command;
using ParkFlow.Application.Features.Cor.Commands.UpdateCorSchedules;
using ParkFlow.Application.Features.Cor.DTOs;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Test.Features.ParkingLogs;
using Xunit;

namespace Test.Features.Onboarding;

public class PersonnelScheduleTests
{
    [Theory]
    [InlineData(Roles.UniversityStaff)]
    [InlineData(Roles.NonAcademicPersonnel)]
    public async Task PersonnelCannotSubmitPersonalScheduleThroughAnyWritePath(Roles role)
    {
        var user = new UserAccount("hash", "+639123456789");
        user.UserProfile = new UserProfile(user.Id, "Employee", "One", null, null);
        user.UserProfile.Personnel = new Personnel(user.UserProfile.Id, "EMP-1", "Office", role);
        var accounts = new Test.Features.Auth.FakeUserAccountRepository();
        await accounts.AddAsync(user);
        var documents = new Test.Features.Cor.FakeCorSubmissionRepository();
        var submission = new CorSubmission(user.Id, "Employee ID", "https://example.test/id.pdf");
        await documents.AddCorSubmissionAsync(submission);
        var schedules = new FakeParkingScheduleRepositoryWithMock([]);
        var previousStep = user.OnboardingStep;

        var onboarding = await new UpdateOnboardingScheduleHandler(documents, schedules, accounts,
            new UpdateOnboardingScheduleValidator()).Handle(new UpdateOnboardingScheduleCommand(user.Id,
                [new ScheduleItem(DayOfWeek.Monday, TimeSpan.FromHours(8), TimeSpan.FromHours(17))]), default);
        Assert.False(onboarding.IsSuccess);
        Assert.Equal(ErrorCode.Forbidden, onboarding.ErrorCode);

        var create = await new CreateParkingScheduleHandler(schedules, new CreateParkingScheduleValidator(), documents, accounts)
            .Handle(new CreateParkingScheduleCommand { SubmissionId = submission.Id, Schedules = [
                new CreateParkingScheduleItem { DayOfWeek = DayOfWeek.Monday, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) }] }, default);
        Assert.False(create.IsSuccess);
        Assert.Equal(ErrorCode.Forbidden, create.ErrorCode);

        var update = await new UpdateCorSchedulesHandler(documents, schedules, userAccountRepository: accounts)
            .Handle(new UpdateCorSchedulesCommand(submission.Id, [new UpdateScheduleItemDto {
                DayOfWeek = 1, StartTime = "08:00", EndTime = "17:00" }]), default);
        Assert.False(update.IsSuccess);
        Assert.Equal(ErrorCode.Forbidden, update.ErrorCode);
        Assert.Equal(previousStep, user.OnboardingStep);
        Assert.Single(documents.Submissions);
    }

    [Fact]
    public async Task StudentScheduleSubmissionStillWorks()
    {
        var user = new UserAccount("hash", "+639123456789");
        user.UserProfile = new UserProfile(user.Id, "Student", "One", null, null);
        var accounts = new Test.Features.Auth.FakeUserAccountRepository();
        await accounts.AddAsync(user);
        var documents = new Test.Features.Cor.FakeCorSubmissionRepository();
        var schedules = new FakeParkingScheduleRepositoryWithMock([]);
        var result = await new UpdateOnboardingScheduleHandler(documents, schedules, accounts,
            new UpdateOnboardingScheduleValidator()).Handle(new UpdateOnboardingScheduleCommand(user.Id,
                [new ScheduleItem(DayOfWeek.Monday, TimeSpan.FromHours(8), TimeSpan.FromHours(17))]), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(OnboardingStep.Done, user.OnboardingStep);
        Assert.Single(documents.Submissions);
    }
}
