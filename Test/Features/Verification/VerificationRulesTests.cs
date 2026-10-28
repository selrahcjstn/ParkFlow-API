using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using Xunit;

namespace Test.Features.Verification;

public class VerificationRulesTests
{
    internal static VerificationSubject Subject(VerificationType type = VerificationType.Cor) => new(
        Guid.NewGuid(), Guid.NewGuid(), type, "fingerprint", true, false, "2023106763", "Juan", "Dela Cruz", "BSCS", false,
        "uploads/parkflow/cor/document.pdf", "2026-2027", "ABC1234", "Honda", true, true,
        [new(DayOfWeek.Monday, TimeSpan.FromHours(8), TimeSpan.FromHours(10))]);
    internal static VerificationPolicy Policy() => new() { AcademicYear = "2026-2027", Semester = "1st Semester" };
    internal const string Text = "Student Name: Dela Cruz, Juan\nStudent Number: 2023-106763\nCourse: BSCS\nAcademic Year: 2026-2027\nSemester: First Semester";

    [Fact]
    public void NormalizedCorMatchesRegistrationAndTerm() => Assert.Empty(CorTextVerification.Compare(CorTextVerification.Extract(Text), Subject(), Policy()));

    [Theory]
    [InlineData("Student Name: Someone Else", "Student name")]
    [InlineData("Student Number: 2023000000", "Student number")]
    [InlineData("Course: BSIT", "Course")]
    [InlineData("Academic Year: 2025-2026", "School year")]
    [InlineData("Semester: Second Semester", "Semester")]
    public void MismatchedFieldsRequireReview(string replacement, string label)
    {
        var lines = Text.Split('\n');
        var key = replacement.Split(':')[0];
        var updated = string.Join('\n', lines.Select(l => l.StartsWith(key + ':') ? replacement : l));
        Assert.Contains(CorTextVerification.Compare(CorTextVerification.Extract(updated), Subject(), Policy()), x => x.Contains(label));
    }

    [Fact]
    public void MissingFieldsRequireReviewAndLegacyPolicyCannotOverrideCurrentTerm()
    {
        Assert.Equal(5, CorTextVerification.Compare(new(null, null, null, null, null), Subject(), Policy()).Count);
        Assert.Empty(CorTextVerification.Compare(CorTextVerification.Extract(Text), Subject(), new()));
    }

    [Theory]
    [InlineData(VerificationType.Account)]
    [InlineData(VerificationType.Cor)]
    public void InformationChecksCannotApproveWithoutTrustedEvidence(VerificationType type)
    {
        Assert.Equal(VerificationState.PreVerified, VerificationService.Decide(type, true, [], false).Status);
        Assert.Equal(VerificationState.Approved, VerificationService.Decide(type, true, [], true).Status);
        Assert.Equal(VerificationState.PreVerified, VerificationService.Decide(type, false, [], true).Status);
        Assert.Equal(VerificationState.NeedsReview, VerificationService.Decide(type, true, ["Missing field"], true).Status);
    }

    [Theory]
    [InlineData(false, VerificationState.PreVerified)]
    [InlineData(true, VerificationState.Approved)]
    public void SchedulePolicyControlsApprovalAfterRulesPass(bool enabled, VerificationState expected) =>
        Assert.Equal(expected, VerificationService.Decide(VerificationType.Schedule, enabled, [], false).Status);

    [Fact]
    public void EveryPolicyDefaultsToDisabled()
    {
        foreach (var type in Enum.GetValues<VerificationType>()) Assert.False(new VerificationPolicy().Allows(type));
    }

    [Fact]
    public void OtpAndValidStudentNumberStillRequireInstitutionalIdentityReview()
    {
        Assert.Empty(VerificationService.MandatoryChecks(Subject(VerificationType.Account), Policy()));
        Assert.Equal(VerificationState.PreVerified, VerificationService.Decide(VerificationType.Account, true, [], false).Status);
        Assert.NotEmpty(VerificationService.MandatoryChecks(Subject(VerificationType.Account) with { EmailVerified = false }, Policy()));
    }

    [Theory]
    [InlineData(VerificationType.Account)]
    [InlineData(VerificationType.Vehicle)]
    public void DuplicateIdentifiersBlockApproval(VerificationType type) =>
        Assert.Contains(VerificationService.MandatoryChecks(Subject(type) with { DuplicateIdentifier = true }, Policy()), x => x.Contains("already registered"));

    [Fact]
    public void UniquePlateDoesNotVerifyOwnership()
    {
        Assert.NotEmpty(VerificationService.MandatoryChecks(Subject(VerificationType.Vehicle) with { OwnershipDocumentsPresent = false }, Policy()));
        Assert.Equal(VerificationState.NeedsReview, VerificationService.Decide(VerificationType.Vehicle, true, [], false).Status);
    }

    [Fact]
    public void ScheduleRulesRejectDuplicatesOverlapsAndInvalidTimesWithoutAnHourCap()
    {
        var schedule = new VerificationSchedule(DayOfWeek.Monday, TimeSpan.FromHours(8), TimeSpan.FromHours(10));
        Assert.NotEmpty(CorTextVerification.CheckSchedules([schedule, schedule]));
        Assert.NotEmpty(CorTextVerification.CheckSchedules([schedule, schedule with { Start = TimeSpan.FromHours(9) }]));
        Assert.NotEmpty(CorTextVerification.CheckSchedules([schedule with { Day = (DayOfWeek)99 }]));
        Assert.NotEmpty(CorTextVerification.CheckSchedules([schedule with { End = TimeSpan.FromHours(7) }]));
        Assert.Empty(CorTextVerification.CheckSchedules([schedule with { End = TimeSpan.FromHours(20) }]));
        Assert.Empty(CorTextVerification.CheckSchedules([schedule, schedule with { Start = TimeSpan.FromHours(10), End = TimeSpan.FromHours(11) }]));
        Assert.NotEmpty(VerificationService.MandatoryChecks(Subject(VerificationType.Schedule) with { CorApproved = false }, Policy()));
    }

    [Fact]
    public void NineHourClassDaysPassEvenWithAnOldEightHourSetting()
    {
        var classes = CorTextVerification.ExtractSchedules(CorScheduleTests.ScheduleTable).Items;
        Assert.Empty(VerificationService.MandatoryChecks(Subject(VerificationType.Schedule) with { Schedules = classes }, Policy()));
    }

    [Fact]
    public void VehicleApprovalStaysManualEvenWithAnOldEnabledToggle()
    {
        Assert.False(new VerificationPolicy { VehicleAutomaticApproval = true }.Allows(VerificationType.Vehicle));
        Assert.Equal(VerificationState.NeedsReview, VerificationService.Decide(VerificationType.Vehicle, true, [], true).Status);
    }

    [Fact]
    public void LegacyEnumValuesAndScheduleApprovalsRemainCompatible()
    {
        Assert.Equal(2, (int)CorVerificationStatus.Verified); Assert.Equal(3, (int)CorVerificationStatus.Rejected);
        var cor = new CorSubmission(Guid.NewGuid(), "2026-2027", "document.pdf", verificationStatus: CorVerificationStatus.Verified);
        Assert.True(cor.HasApprovedSchedule);
        cor.UpdateScheduleVerification(CorVerificationStatus.Pending);
        Assert.False(cor.HasApprovedSchedule); Assert.Equal(CorVerificationStatus.Verified, cor.VerificationStatus);
    }

    [Fact]
    public void RejectingVehicleOrCorDoesNotChangeAccount()
    {
        var account = new UserAccount("password", "phone"); account.Verify();
        var cor = new CorSubmission(account.Id, "2026-2027", "document.pdf");
        var vehicle = new Vehicle(account.Id, "ABC1234", "Honda", "hash", VehicleType.Motorcycle);
        cor.UpdateSubmission(null, null, CorVerificationStatus.Rejected, rejectionReason: "Unreadable");
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Rejected, "Ownership not established");
        Assert.Equal(AccountStatus.Active, account.Status);
    }
}
