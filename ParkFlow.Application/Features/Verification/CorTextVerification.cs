using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Verification;

public record CorFields(string? Name, string? StudentNumber, string? Course, string? AcademicYear, string? Semester,
    string? Section = null, string? YearLevel = null)
{
    public List<VerificationSchedule> Schedules { get; init; } = [];
    public List<string> ScheduleIssues { get; init; } = [];
    public string? College { get; init; }
    public string? SourceDocumentUrl { get; init; }
    public string? ConfirmedCollege { get; init; }
    public ConfirmedCorStudentInformation? ConfirmedInformation { get; init; }
    public List<CorWeekDay>? ConfirmedWeek { get; init; }
    public List<CorWeekDay>? AdminWeek { get; init; }
    public bool ScheduleReviewRequested { get; init; }
    public string? ScheduleReviewReason { get; init; }
}
public static class CorTextVerification
{
    private const string Boundaries = @"Student\s*(?:Name|No\.?|Number|ID)|Name|College|Program|Course|Degree|School\s+Year|Academic\s+Year|Semester|Term|Curriculum|Major|Year\s+Level|Section|Financial\s+Assistance|Gender|Age";
    private const string Days = @"Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday|Mon|Tue(?:s)?|Wed|Thu(?:rs)?|Fri|Sat|Sun|Th|Su|M|T|W|F|S";
    private const string Time = @"\d{1,2}:\d{2}\s*(?:[AP]\.?M\.?)?";
    private static Regex Pattern(string value) => new(value, RegexOptions.Compiled | RegexOptions.CultureInvariant, System.TimeSpan.FromSeconds(1));
    private static readonly Regex NonLetters = Pattern(@"[^\p{L}\p{N}]+");
    private static readonly Regex Whitespace = Pattern(@"\s+");
    private static readonly Regex InformationStart = Pattern(@"(?i)Student\s+General\s+Information");
    private static readonly Regex TableStart = Pattern(@"(?i)\b(?:Registered\s+(?:Courses|Subjects)|Class\s+Schedule)\b");
    private static readonly Regex TableEnd = Pattern(@"(?i)\b(?:Assessed\s+Fees|Rules\s+Governing\s+Refund|Total\s+Assessment)\b");
    private static readonly Regex Labels = Pattern(@"(?im)\b(?<label>Student\s*ID\s*(?:Number|No\.?)|Class\s*Section|A\.?Y\.?|" + Boundaries + @")(?:[ \t]*[:=][ \t]*|[ \t]+)");
    private static readonly Regex WrappedProgram = Pattern(@"(?im)\bProgram(?:[ \t]*[:=][ \t]*|[ \t]+)([^\r\n]+)\r?\n(?!\s*(?:" + Boundaries + @")\b)\s*([^\r\n]+)");
    private static readonly Regex NextLabel = Pattern(@"(?i)\s+(?:" + Boundaries + @")\b(?:\s*[:=]|\s+)");
    private static readonly Regex AcademicYear = Pattern(@"\b\d{4}\s*-\s*\d{4}\b");
    private static readonly Regex CombinedSemester = Pattern(@"(?im)(?:School|Academic)\s+Year[^\r\n]*?\b(?<term>(?:1st|2nd|First|Second)\s+Semester|Summer)\b");
    private static readonly Regex Level = Pattern(@"^\d+");
    private static readonly Regex DayFirst = Pattern(@"(?im)(?<![\p{L}\p{N}])(?<day>" + Days + @")\s+(?<start>" + Time + @")\s*[-–—]\s*(?<end>" + Time + @")(?![\p{L}\p{N}])");
    private static readonly Regex TimeFirst = Pattern(@"(?im)(?<start>" + Time + @")\s*[-–—]\s*(?<end>" + Time + @")\s+(?<day>" + Days + @")(?![\p{L}\p{N}])");
    private static readonly Regex ColumnHeader = Pattern(@"(?i)Start\s*(?:Time)?\s+End\s*(?:Time)?");
    private static readonly Regex TimeColumns = Pattern(@"(?im)(?<![\p{L}\p{N}])(?<day>" + Days + @")\s+(?<start>" + Time + @")\s+(?<end>" + Time + @")(?![\p{L}\p{N}])");
    private static readonly Regex Ranges = Pattern(@"(?i)" + Time + @"\s*[-–—]\s*" + Time);
    private static readonly Regex Times = Pattern(@"(?i)" + Time);
    private static readonly string[] TimeFormats = ["h:mmtt", "hh:mmtt", "H:mm", "HH:mm"];

    public static string Normalize(string? value) => NonLetters.Replace((value ?? "").Normalize(NormalizationForm.FormKC).ToUpperInvariant(), " ").Trim();
    public static string Compact(string? value) => Normalize(value).Replace(" ", "");

    public static CorFields Extract(string text)
    {
        // OCR can place the three information columns on the same line. Bound each
        // value by the next label rather than absorbing neighbouring student fields.
        var information = text.Normalize(NormalizationForm.FormKC);
        var infoStart = InformationStart.Match(information);
        if (infoStart.Success) information = information[(infoStart.Index + infoStart.Length)..];
        var infoEnd = TableStart.Match(information);
        if (infoEnd.Success) information = information[..infoEnd.Index];
        string Clean(string value) { var next = NextLabel.Match(value); return (next.Success ? value[..next.Index] : value).Trim(); }
        // Match labels once, then take values up to the next label or line end.
        // This preserves inline OCR columns without eight full document scans.
        var values = new Dictionary<string, string>();
        var labels = Labels.Matches(information);
        for (var i = 0; i < labels.Count; i++)
        {
            var match = labels[i]; var start = match.Index + match.Length;
            var lineEnd = information.AsSpan(start).IndexOfAny('\r', '\n'); var end = lineEnd < 0 ? information.Length : start + lineEnd;
            if (i + 1 < labels.Count && labels[i + 1].Index < end) end = labels[i + 1].Index;
            var value = information[start..end].Trim(); if (value.Length == 0) continue;
            var key = Whitespace.Replace(match.Groups["label"].Value.ToUpperInvariant().Replace(".", ""), "");
            key = key switch { "STUDENTNAME" => "NAME", "STUDENTNO" or "STUDENTID" or "STUDENTIDNUMBER" or "STUDENTIDNO" => "STUDENTNUMBER",
                "PROGRAM" or "DEGREE" => "COURSE", "SCHOOLYEAR" or "AY" => "ACADEMICYEAR", "TERM" => "SEMESTER", "CLASSSECTION" => "SECTION", _ => key };
            values.TryAdd(key, value);
        }
        string? Field(string key) => values.GetValueOrDefault(key);
        var result = new CorFields(Field("NAME"), Field("STUDENTNUMBER"), Field("COURSE"), Field("ACADEMICYEAR"), Field("SEMESTER"), Field("SECTION"), Field("YEARLEVEL"));
        var schedule = ExtractSchedules(text);
        // The university's digital COR has unpunctuated labels and a wrapped Program.
        var program = WrappedProgram.Match(information);
        if (program.Success) result = result with { Course = Clean(program.Groups[1].Value) + " " + Clean(program.Groups[2].Value) };
        var year = AcademicYear.Match(result.AcademicYear ?? "");
        var semester = CombinedSemester.Match(information);
        var level = Level.Match(result.YearLevel ?? "");
        return result with { AcademicYear = year.Success ? year.Value.Replace(" ", "") : result.AcademicYear,
            Semester = result.Semester ?? (semester.Success ? semester.Groups["term"].Value : null),
            Section = CorStudentMapping.Section(result.Section),
            YearLevel = level.Success ? level.Value : result.YearLevel, College = Field("COLLEGE"), Schedules = schedule.Items, ScheduleIssues = schedule.Issues };
    }

    public static (List<VerificationSchedule> Items, List<string> Issues) ExtractSchedules(string text)
    {
        var tableStart = TableStart.Match(text);
        if (tableStart.Success)
        {
            text = text[(tableStart.Index + tableStart.Length)..];
            var tableEnd = TableEnd.Match(text);
            if (tableEnd.Success) text = text[..tableEnd.Index];
        }
        // BulSU CORs use M, T, W, Th, F, S and Su before each class time.
        // Retain raw class sessions as extraction metadata; the parking preview
        // aggregates these into one earliest-to-latest interval per day.
        var items = new List<VerificationSchedule>();
        var issues = new List<string>();
        var matches = DayFirst.Matches(text).Cast<Match>().ToList();
        matches.AddRange(TimeFirst.Matches(text).Cast<Match>()
            .Where(m => !matches.Any(p => m.Index < p.Index + p.Length && p.Index < m.Index + m.Length)).ToList());
        if (ColumnHeader.IsMatch(text))
        {
            matches.AddRange(TimeColumns.Matches(text).Cast<Match>()
                .Where(m => !matches.Any(p => m.Index < p.Index + p.Length && p.Index < m.Index + m.Length)).ToList());
        }
        foreach (var match in matches)
        {
            var day = match.Groups["day"].Value.ToUpperInvariant();
            var number = day switch
            {
                "M" or "MON" or "MONDAY" => DayOfWeek.Monday,
                "T" or "TUE" or "TUES" or "TUESDAY" => DayOfWeek.Tuesday,
                "W" or "WED" or "WEDNESDAY" => DayOfWeek.Wednesday,
                "TH" or "THU" or "THURS" or "THURSDAY" => DayOfWeek.Thursday,
                "F" or "FRI" or "FRIDAY" => DayOfWeek.Friday,
                "S" or "SAT" or "SATURDAY" => DayOfWeek.Saturday,
                _ => DayOfWeek.Sunday
            };
            if (!TryTime(match.Groups["start"].Value, out var start) || !TryTime(match.Groups["end"].Value, out var end) || end <= start)
                issues.Add("A class time could not be read reliably. Upload a clearer COR.");
            else items.Add(new(number, start, end));
        }
        if (items.Count == 0) issues.Add("No class schedule could be read. Upload a COR with a clear schedule table.");
        // A partially recognized table must not silently omit class rows.
        if (Ranges.Count(text) > items.Count || Times.Count(text) > items.Count * 2)
            issues.Add("Some class rows could not be read. Upload a clearer COR with complete days and times.");
        // Class sessions can overlap (for example combined subject rows). Parking
        // validation runs after they are combined into one daily attendance window.
        return (items.OrderBy(s => ((int)s.Day + 6) % 7).ThenBy(s => s.Start).ToList(), issues.Distinct().ToList());

        static bool TryTime(string text, out TimeSpan time)
        {
            text = Whitespace.Replace(text.ToUpperInvariant().Replace(".", ""), "").Trim();
            if (DateTime.TryParseExact(text, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            { time = parsed.TimeOfDay; return true; }
            time = default; return false;
        }
    }

    public static string SemesterKey(string? value)
    {
        var normalized = Normalize(value);
        if (Regex.IsMatch(normalized, @"^(1|1ST|FIRST)( SEMESTER)?$")) return "1";
        if (Regex.IsMatch(normalized, @"^(2|2ND|SECOND)( SEMESTER)?$")) return "2";
        if (normalized is "SUMMER" or "MIDYEAR" or "MID YEAR") return "SUMMER";
        return normalized;
    }

    public static List<string> Compare(CorFields fields, VerificationSubject subject, VerificationPolicy policy)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(fields.Name)) issues.Add("Student name is missing.");
        else
        {
            var names = Normalize(fields.Name).Split(' ').Order().ToArray();
            // Middle names may appear; every registered first/last name token must match.
            if (!Normalize(subject.FirstName + " " + subject.LastName).Split(' ').All(names.Contains))
                issues.Add("Student name does not match registration.");
        }
        Check(fields.StudentNumber, subject.Identifier, "Student number", Compact);
        var courseAbbreviation = Regex.Match(subject.Course ?? "", @"\(([^)]+)\)").Groups[1].Value;
        if (string.IsNullOrWhiteSpace(courseAbbreviation) || Compact(courseAbbreviation) != Compact(fields.Course))
            Check(fields.Course, subject.Course, "Course", CourseKey);
        issues.AddRange(CorAcademicTerm.Validate(fields));
        if (!string.IsNullOrWhiteSpace(fields.Section)) Check(CorStudentMapping.Section(fields.Section), CorStudentMapping.Section(subject.Section), "Section", Normalize);
        if (!string.IsNullOrWhiteSpace(fields.YearLevel)) Check(fields.YearLevel, subject.YearLevel?.ToString(CultureInfo.InvariantCulture), "Year level", Compact);
        return issues;

        void Check(string? actual, string? expected, string label, Func<string?, string> normalize)
        {
            if (string.IsNullOrWhiteSpace(actual)) issues.Add(label + " is missing.");
            else if (string.IsNullOrWhiteSpace(expected) || normalize(actual) != normalize(expected))
                issues.Add(label + " does not match registration or university policy.");
        }
    }

    public static string CourseKey(string? value)
    {
        var course = Regex.Replace(value ?? "", @"\([^)]*\)", "");
        course = Normalize(course).Replace("BACHELOR OF SCIENCE", "BS").Replace("BACHELOR OF ARTS", "BA");
        return Regex.Replace(course, @"^(BS|BA) IN ", "$1 ");
    }

    public static List<string> CheckSchedules(IEnumerable<VerificationSchedule> schedules)
    {
        var list = schedules.ToList();
        var issues = new List<string>();
        if (list.Count == 0) issues.Add("At least one schedule is required.");
        if (list.Any(s => !Enum.IsDefined(s.Day) || s.Start < TimeSpan.Zero || s.End >= TimeSpan.FromDays(1) || s.End <= s.Start))
            issues.Add("Select valid days and times with start before end.");
        foreach (var group in list.GroupBy(s => s.Day))
        {
            var ordered = group.OrderBy(s => s.Start).ToList();
            if (ordered.Zip(ordered.Skip(1)).Any(pair => pair.First.End > pair.Second.Start))
                issues.Add("Schedules overlap or are duplicated.");
        }
        return issues.Distinct().ToList();
    }
}
