using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using Xunit;

namespace Test.Features.Verification;

public class CorScheduleTests
{
    internal const string ScheduleTable = """
        Registered Courses
        Course Code Course Description LEC LAB Credit Section Schedule Building/Room Instructor Remarks
        CAP 401W Research 2.0 1.0 3.0 BSIT Th 02:00 PM-05:00 PM
        T 07:00 AM-09:00 AM N/A
        IT 404 System Administration M 10:00 AM-01:00 PM
        T 11:00 AM-01:00 PM
        IT 403 Security F 10:00 AM-01:00 PM
        Th 07:00 AM-09:00 AM
        IT 402 Specialization F 01:00 PM-03:00 PM
        M 02:00 PM-05:00 PM
        IT 401 Specialization T 02:00 PM-04:00 PM
        M 07:00 AM-10:00 AM
        MST 101 Environmental Science T 04:00 PM-07:00 PM
        Assessed Fees
        """;

    [Fact]
    public void UniversityTableKeepsAllElevenIntervalsAndGaps()
    {
        var parsed = CorTextVerification.ExtractSchedules(ScheduleTable);
        Assert.Empty(parsed.Issues); Assert.Equal(11, parsed.Items.Count);
        Assert.Equal(3, parsed.Items.Count(s => s.Day == DayOfWeek.Monday));
        Assert.Equal(4, parsed.Items.Count(s => s.Day == DayOfWeek.Tuesday));
        Assert.Equal(2, parsed.Items.Count(s => s.Day == DayOfWeek.Thursday));
        Assert.Equal(2, parsed.Items.Count(s => s.Day == DayOfWeek.Friday));
        Assert.Contains(parsed.Items, s => s.Day == DayOfWeek.Monday && s.Start == TimeSpan.FromHours(14) && s.End == TimeSpan.FromHours(17));
    }

    [Fact]
    public void UniversityUnpunctuatedLabelsAndWrappedProgramAreExtracted()
    {
        var fields = CorTextVerification.Extract("Student No. 2023000001\nName Example, Juan\nCollege CICT\nProgram Bachelor of Science in\nInformation Technology\nMajor\nYear Level 4th Year\nSchool Year 2026-2027 1st Semester\nCurriculum BSIT\nSection BSIT 4A\n" + ScheduleTable);
        Assert.Equal("2023000001", fields.StudentNumber); Assert.Equal("Example, Juan", fields.Name);
        Assert.Equal("Bachelor of Science in Information Technology", fields.Course);
        Assert.Equal("2026-2027", fields.AcademicYear); Assert.Equal("1st Semester", fields.Semester);
        Assert.Equal("4", fields.YearLevel); Assert.Equal(11, fields.Schedules.Count);
    }

    [Theory]
    [InlineData("Monday 13:00-12:00")]
    [InlineData("Monday 08:00-10:00\nUnknown 11:00-13:00")]
    [InlineData("M 08:00 AM-10:00 AM\nTh 11:00 AM 01:00 PM")]
    [InlineData("No schedule table")]
    public void IncompleteAndInvalidRowsCannotBeConfirmed(string text) => Assert.NotEmpty(CorTextVerification.ExtractSchedules(text).Issues);

    [Fact]
    public void DailyWindowsIncludeGapsAndKeepSevenDaysWithoutInventingTimes()
    {
        var fields = CorTextVerification.Extract(ScheduleTable);
        var daily = CorStudentMapping.Aggregate(fields.Schedules);
        Assert.Equal(4, daily.Count);
        Assert.Equal(new VerificationSchedule(DayOfWeek.Monday, TimeSpan.FromHours(7), TimeSpan.FromHours(17)), daily[0]);
        Assert.Equal(new VerificationSchedule(DayOfWeek.Tuesday, TimeSpan.FromHours(7), TimeSpan.FromHours(19)), daily[1]);
        Assert.Equal(new VerificationSchedule(DayOfWeek.Thursday, TimeSpan.FromHours(7), TimeSpan.FromHours(17)), daily[2]);
        Assert.Equal(new VerificationSchedule(DayOfWeek.Friday, TimeSpan.FromHours(10), TimeSpan.FromHours(15)), daily[3]);
        var week = CorStudentMapping.Week(fields);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 0 }, week.Select(d => d.DayOfWeek));
        Assert.All(week.Where(d => d.HasClasses == false), d => { Assert.Null(d.StartTime); Assert.Null(d.EndTime); });
        Assert.Equal(3, week.Count(d => d.HasClasses == false));
        Assert.Empty(CorStudentMapping.ValidateWeek(week, out var saved)); Assert.Equal(daily, saved);
    }

    [Fact]
    public void OverlappingAndDuplicateSubjectSessionsProduceOneDailyWindow()
    {
        var fields = CorTextVerification.Extract("T 08:00-10:00\nT 09:00-11:00\nT 08:00-10:00");
        Assert.Empty(fields.ScheduleIssues);
        var daily = Assert.Single(CorStudentMapping.Aggregate(fields.Schedules));
        Assert.Equal(TimeSpan.FromHours(8), daily.Start); Assert.Equal(TimeSpan.FromHours(11), daily.End);
    }

    [Theory]
    [InlineData("Registered Courses\nIT 101 07:00 AM-09:00 AM T\nIT 102 02:00 PM-05:00 PM Th")]
    [InlineData("Registered Subjects\nDay Start Time End Time\nT 07:00 AM 09:00 AM\nTh 02:00 PM 05:00 PM")]
    [InlineData("Class Schedule\nT 07:00-09:00\nTh 14:00-17:00")]
    public void AlternativeTableLayoutsDistinguishTuesdayAndThursday(string text)
    {
        var parsed = CorTextVerification.ExtractSchedules(text); Assert.Empty(parsed.Issues);
        Assert.Equal(DayOfWeek.Tuesday, parsed.Items[0].Day); Assert.Equal(DayOfWeek.Thursday, parsed.Items[1].Day);
        Assert.Equal(TimeSpan.FromHours(14), parsed.Items[1].Start);
    }

    [Theory]
    [InlineData("S", 6)] [InlineData("Sat", 6)] [InlineData("Su", 0)] [InlineData("Sun", 0)]
    public void WeekendAbbreviationsAreRecognized(string day, int expected) =>
        Assert.Equal((DayOfWeek)expected, Assert.Single(CorTextVerification.ExtractSchedules(day + " 08:00-10:00").Items).Day);

    [Fact]
    public void UnknownRowsRemainUnknownUntilEveryDayIsReviewed()
    {
        var week = CorStudentMapping.Week(CorTextVerification.Extract("M 08:00-10:00\nUnknown 11:00-12:00"));
        Assert.Equal(6, week.Count(d => d.HasClasses == null));
        Assert.NotEmpty(CorStudentMapping.ValidateWeek(week, out _));
        var corrected = week.Select(d => d.HasClasses == null ? d with { HasClasses = false } : d).ToList();
        corrected[0] = corrected[0] with { StartTime = "07:00", EndTime = "17:00" };
        Assert.Empty(CorStudentMapping.ValidateWeek(corrected, out var intervals));
        Assert.Equal(TimeSpan.FromHours(17), Assert.Single(intervals).End);
        Assert.NotEmpty(CorStudentMapping.ValidateWeek(corrected[..6], out _));
        corrected[0] = corrected[0] with { EndTime = "06:00" };
        Assert.NotEmpty(CorStudentMapping.ValidateWeek(corrected, out _));
    }

    [Theory]
    [InlineData("Dela Cruz, Juan Miguel R.", "Juan Miguel", "Dela Cruz", "R.")]
    [InlineData("Santos, Maria Ana", "Maria Ana", "Santos", null)]
    [InlineData("Juan Santos", null, null, null)]
    [InlineData("", null, null, null)]
    public void NamesDoNotGuessAmbiguousMiddleNames(string printed, string? first, string? last, string? middle)
    {
        var mapped = CorStudentMapping.Information(new(printed, null, null, null, null));
        Assert.Equal(first, mapped.FirstName); Assert.Equal(last, mapped.LastName); Assert.Equal(middle, mapped.MiddleName);
        Assert.Null(mapped.StudentNumber); Assert.Null(mapped.Course); Assert.Null(mapped.YearLevel);
    }

    [Fact]
    public void InlineOcrColumnsKeepStudentFieldsSeparate()
    {
        var fields = CorTextVerification.Extract("Student General Information\nStudent No. 2023000001 Program Bachelor of Science in Information Technology College CICT\nName Example, Juan R. Year Level 4th Year\nSchool Year 2026-2027 1st Semester Section BSIT 4A\n" + ScheduleTable);
        Assert.Equal("2023000001", fields.StudentNumber); Assert.Equal("Example, Juan R.", fields.Name);
        Assert.Equal("Bachelor of Science in Information Technology", fields.Course); Assert.Equal("CICT", fields.College);
        Assert.Equal("2026-2027", fields.AcademicYear); Assert.Equal("1st Semester", fields.Semester); Assert.Equal("BSIT 4A", fields.Section);
        Assert.Equal(CorTextVerification.CourseKey(fields.Course), CorTextVerification.CourseKey("BS Information Technology (BSIT)"));
    }

    [Fact]
    public void PrintedCourseAbbreviationMatchesExistingDropdownWithoutGuessingAnAcronym()
    {
        var fields = CorTextVerification.Extract(VerificationRulesTests.Text.Replace("BSCS", "BSIT"));
        Assert.Empty(CorTextVerification.Compare(fields, VerificationRulesTests.Subject() with { Course = "BS Information Technology (BSIT)" }, VerificationRulesTests.Policy()));
        Assert.Contains(CorTextVerification.Compare(fields with { Course = "Unknown" }, VerificationRulesTests.Subject() with { Course = "BS Information Technology (BSIT)" }, VerificationRulesTests.Policy()), issue => issue.StartsWith("Course"));
    }

    [Fact]
    public void ExitUsesEntryClassAndExtendsOnlyAdjacentClasses()
    {
        var id = Guid.NewGuid();
        var schedules = new[] { new ParkingSchedule(id, DayOfWeek.Monday, TimeSpan.FromHours(7), TimeSpan.FromHours(10)),
            new ParkingSchedule(id, DayOfWeek.Monday, TimeSpan.FromHours(10), TimeSpan.FromHours(13)),
            new ParkingSchedule(id, DayOfWeek.Monday, TimeSpan.FromHours(14), TimeSpan.FromHours(17)) };
        Assert.Equal(TimeSpan.FromHours(13), ScheduleService.FindEntrySchedule(schedules, new DateTime(2026, 10, 12, 8, 0, 0))!.EndTime);
        Assert.Equal(TimeSpan.FromHours(17), ScheduleService.FindEntrySchedule(schedules, new DateTime(2026, 10, 12, 15, 0, 0))!.EndTime);
    }
}
