using System.Globalization;
using System.Text.RegularExpressions;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Verification;

public record CorWeekDay(int DayOfWeek, bool? HasClasses, string? StartTime, string? EndTime);
public record CorStudentInformation(string? FullName, string? FirstName, string? LastName, string? MiddleName,
    string? StudentNumber, string? College, string? Course, int? YearLevel, string? Section, string? AcademicYear, string? Semester);
public record ConfirmedCorStudentInformation(string FirstName, string LastName, string? MiddleName, string PhoneNumber,
    string StudentNumber, string? College, string? Course, int YearLevel, string Section, string AcademicYear, string Semester);
public record CorStudentConfirmation(Guid RecordId, int Version, ConfirmedCorStudentInformation Information, List<CorWeekDay>? Week = null);

public static class CorStudentMapping
{
    public static string? Section(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        // Keep the printed section code, excluding a trailing specialization such as WMAD.
        var match = Regex.Match(value, @"\b\d{1,2}[A-Z][A-Z0-9]*\s*-\s*G\d+\b", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        return match.Success ? Regex.Replace(match.Value, @"\s+", "").ToUpperInvariant() : value.Trim();
    }
    public static List<CorWeekDay> AuthoritativeWeek(CorFields fields) => fields.AdminWeek ?? Week(fields);
    public static List<VerificationSchedule> ConfirmableSchedules(CorFields fields, List<CorWeekDay>? supplied = null)
    {
        var week = AuthoritativeWeek(fields);
        if (supplied != null && (ValidateWeek(supplied, out _).Count > 0 || !supplied.OrderBy(d => d.DayOfWeek).SequenceEqual(week.OrderBy(d => d.DayOfWeek))))
            throw new DocumentValidationException("Schedules are read-only. Upload a clearer COR if the schedule is incorrect.");
        var errors = ValidateWeek(week, out var intervals);
        if (fields.AdminWeek == null && fields.ScheduleIssues.Count > 0 || errors.Count > 0)
            throw new DocumentValidationException("The schedule could not be read completely. Upload a clearer COR to continue. Your personal information remains editable.");
        return intervals;
    }
    public static CorStudentInformation Information(CorFields fields)
    {
        string? first = null, last = null, middle = null;
        var name = (fields.Name ?? "").Split(',');
        if (name.Length == 2 && !string.IsNullOrWhiteSpace(name[0]) && !string.IsNullOrWhiteSpace(name[1]))
        {
            last = name[0].Trim();
            var given = Regex.Split(name[1].Trim(), @"\s+");
            // Preserve a printed initial; never expand it or guess a full middle name.
            if (given.Length > 1 && Regex.IsMatch(given[^1], @"^[\p{L}]\.?$"))
            { middle = given[^1]; first = string.Join(" ", given[..^1]); }
            else first = string.Join(" ", given);
        }
        return new(fields.Name, first, last, middle,
            string.IsNullOrWhiteSpace(fields.StudentNumber) ? null : Student.NormalizeNumber(fields.StudentNumber),
            fields.ConfirmedCollege ?? fields.College, fields.Course,
            int.TryParse(fields.YearLevel, out var year) ? year : null, Section(fields.Section), fields.AcademicYear, fields.Semester);
    }

    public static List<VerificationSchedule> Aggregate(IEnumerable<VerificationSchedule> classes)
    {
        Span<TimeSpan> starts = stackalloc TimeSpan[7], ends = stackalloc TimeSpan[7];
        Span<bool> seen = stackalloc bool[7]; seen.Clear();
        foreach (var item in classes)
        {
            var day = (int)item.Day;
            if (day is < 0 or > 6 || item.Start < TimeSpan.Zero || item.End >= TimeSpan.FromDays(1) || item.End <= item.Start) continue;
            if (!seen[day] || item.Start < starts[day]) starts[day] = item.Start;
            if (!seen[day] || item.End > ends[day]) ends[day] = item.End;
            seen[day] = true;
        }
        var result = new List<VerificationSchedule>(7);
        for (var order = 0; order < 7; order++)
        {
            var day = (order + 1) % 7;
            if (seen[day]) result.Add(new((DayOfWeek)day, starts[day], ends[day]));
        }
        return result;
    }

    public static List<CorWeekDay> Week(CorFields fields, List<VerificationSchedule>? intervals = null)
    {
        intervals ??= Aggregate(fields.Schedules);
        return new[] { 1, 2, 3, 4, 5, 6, 0 }.Select(day =>
        {
            var interval = intervals.FirstOrDefault(c => (int)c.Day == day);
            return interval != null ? new CorWeekDay(day, true, Format(interval.Start), Format(interval.End))
                : new CorWeekDay(day, fields.ScheduleIssues.Count == 0 ? false : null, null, null);
        }).ToList();
    }

    public static List<string> ValidateWeek(List<CorWeekDay>? week, out List<VerificationSchedule> intervals)
    {
        intervals = [];
        if (week == null || week.Count != 7 || week.Any(w => w == null) || week.Select(w => w.DayOfWeek).Distinct().Count() != 7 || week.Any(w => w.DayOfWeek is < 0 or > 6))
            return ["Review all seven days, using each day once."];
        var errors = new List<string>();
        foreach (var day in week)
        {
            if (day.HasClasses == null) { errors.Add("Choose class times or No Classes for every unreadable day."); continue; }
            if (day.HasClasses == false)
            {
                if (!string.IsNullOrWhiteSpace(day.StartTime) || !string.IsNullOrWhiteSpace(day.EndTime)) errors.Add("No Classes days must not include times.");
                continue;
            }
            if (!TimeSpan.TryParseExact(day.StartTime, [@"hh\:mm", @"hh\:mm\:ss"], CultureInfo.InvariantCulture, out var start) ||
                !TimeSpan.TryParseExact(day.EndTime, [@"hh\:mm", @"hh\:mm\:ss"], CultureInfo.InvariantCulture, out var end))
            { errors.Add("Enter valid start and end times for each class day."); continue; }
            intervals.Add(new((DayOfWeek)day.DayOfWeek, start, end));
        }
        errors.AddRange(CorTextVerification.CheckSchedules(intervals));
        return errors.Distinct().ToList();
    }

    public static string Format(TimeSpan time) => time.ToString(@"hh\:mm\:ss");
}
