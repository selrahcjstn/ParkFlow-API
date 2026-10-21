using System.Text.RegularExpressions;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Verification;

// The super admin's System Settings are the source of the current academic term.
public static class CorAcademicTerm
{
    public static bool IsValid(string? year, string? semester) =>
        Regex.IsMatch(year ?? "", @"^\d{4}-\d{4}$") &&
        int.TryParse(year![..4], out var start) && int.TryParse(year[5..], out var end) && end == start + 1 &&
        CorTextVerification.SemesterKey(semester) is "1" or "2" or "SUMMER";

    public static List<string> Validate(CorFields fields, string? submittedYear = null, string? submittedSemester = null)
    {
        var current = SystemSettingsStore.Current;
        var errors = new List<string>();
        if (!IsValid(current.AcademicYear, current.CurrentSemester))
            return ["The current school year and semester have not been configured. Please contact the super admin."];
        if (string.IsNullOrWhiteSpace(fields.AcademicYear))
            errors.Add($"School year could not be read from the COR. The current school year is {current.AcademicYear}.");
        else if (CorTextVerification.Compact(fields.AcademicYear) != CorTextVerification.Compact(current.AcademicYear))
            errors.Add($"School year mismatch: your COR shows {fields.AcademicYear}; the current school year is {current.AcademicYear}.");
        if (string.IsNullOrWhiteSpace(fields.Semester))
            errors.Add($"Semester could not be read from the COR. The current semester is {current.CurrentSemester}.");
        else if (CorTextVerification.SemesterKey(fields.Semester) != CorTextVerification.SemesterKey(current.CurrentSemester))
            errors.Add($"Semester mismatch: your COR shows {fields.Semester}; the current semester is {current.CurrentSemester}.");
        if (submittedYear != null && CorTextVerification.Compact(submittedYear) != CorTextVerification.Compact(fields.AcademicYear) ||
            submittedSemester != null && CorTextVerification.SemesterKey(submittedSemester) != CorTextVerification.SemesterKey(fields.Semester))
            errors.Add("School year and semester are read-only and must match the uploaded COR.");
        return errors;
    }

    public static CorSchedulePreview Preview(CorSchedulePreview preview, CorFields? fields)
    {
        var current = SystemSettingsStore.Current;
        var errors = fields == null ? new List<string>() : Validate(fields);
        return preview with { CurrentAcademicYear = current.AcademicYear, CurrentSemester = current.CurrentSemester,
            Status = errors.Count > 0 ? "Rejected" : preview.Status,
            CanConfirm = preview.CanConfirm && errors.Count == 0,
            CanConfirmSchedule = preview.CanConfirmSchedule && errors.Count == 0,
            Issues = preview.Issues.Concat(errors).Distinct().ToList() };
    }
}
