using MediatR;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingProfile;
using ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingStudent;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Verification;

public record VerificationDto(Guid Id, Guid UserAccountId, Guid SubjectId, string Type, string Status, string? Method,
    string Reason, string? OcrOutcome, string? ExtractedFieldsJson, DateTime? VerifiedAt, Guid? ReviewerId,
    int Attempts, int Version, bool IsCurrent, DateTime CreatedAt);
public record GetVerificationPolicy : IRequest<VerificationPolicy>;
public record UpdateVerificationPolicy(VerificationPolicy Policy, Guid Actor) : IRequest<Result<bool>>;
public record ListVerifications(Guid? UserId, bool History, bool IncludeExtractedFields = false) : IRequest<List<VerificationDto>>;
public record ListVerificationAudit(Guid? RecordId) : IRequest<List<VerificationAudit>>;
public record ReviewVerification(Guid Id, int Version, string Status, string Reason, Guid Actor) : IRequest<Result<bool>>;
public record ReviewVerificationSubject(string Type, Guid SubjectId, string Status, string Reason, Guid Actor) : IRequest<Result<bool>>;
public record FetchCorInformation(Guid Owner, Guid RecordId, int Version) : IRequest<Result<CorSchedulePreview>>;
public record GetCorSchedulePreview(Guid Owner) : IRequest<CorSchedulePreview?>;
public record ConfirmCorSchedule(Guid RecordId, int Version, Guid Owner, List<CorWeekDay>? Week = null) : IRequest<Result<bool>>;
public record ConfirmStudentFromCor(Guid Owner, CorStudentConfirmation Confirmation) : IRequest<Result<bool>>;
public record RequestCorScheduleReview(Guid Owner, Guid RecordId, int Version, string Reason) : IRequest<Result<bool>>;
public record CorrectCorSchedule(Guid RecordId, int Version, Guid Actor, List<CorWeekDay> Week, string Reason, bool CorReviewed) : IRequest<Result<bool>>;
public record CorScheduleItem(int DayOfWeek, string StartTime, string EndTime);
public record CorSchedulePreview(Guid RecordId, int Version, string Status, string DocumentUrl,
    List<CorScheduleItem> Items, List<string> Issues, bool CanConfirm)
{
    public List<CorWeekDay> Week { get; init; } = [];
    public CorStudentInformation? StudentInformation { get; init; }
    public bool InformationConfirmed { get; init; }
    public string? PhoneNumber { get; init; }
    public bool ScheduleReviewRequested { get; init; }
    public bool CanConfirmSchedule { get; init; }
    public bool FetchRequested { get; init; }
    public string? CurrentAcademicYear { get; init; }
    public string? CurrentSemester { get; init; }
}

public class VerificationRequests(IVerificationStore store, VerificationService service, ILogger<VerificationRequests>? logger = null, IUserAccountRepository? accounts = null) :
    IRequestHandler<GetVerificationPolicy, VerificationPolicy>,
    IRequestHandler<UpdateVerificationPolicy, Result<bool>>,
    IRequestHandler<ListVerifications, List<VerificationDto>>,
    IRequestHandler<ListVerificationAudit, List<VerificationAudit>>,
    IRequestHandler<ReviewVerification, Result<bool>>,
    IRequestHandler<ReviewVerificationSubject, Result<bool>>,
    IRequestHandler<GetCorSchedulePreview, CorSchedulePreview?>,
    IRequestHandler<FetchCorInformation, Result<CorSchedulePreview>>,
    IRequestHandler<ConfirmCorSchedule, Result<bool>>,
    IRequestHandler<ConfirmStudentFromCor, Result<bool>>,
    IRequestHandler<RequestCorScheduleReview, Result<bool>>,
    IRequestHandler<CorrectCorSchedule, Result<bool>>
{
    public const string AwaitingInformationFetch = "AwaitingInformationFetch";

    public async Task<CorSchedulePreview?> Handle(GetCorSchedulePreview request, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        try { return await PreviewAsync(request.Owner, ct); }
        finally { logger?.LogInformation("COR preview API/database/mapping in {DurationMs} ms", watch.Elapsed.TotalMilliseconds); }
    }

    public async Task<Result<CorSchedulePreview>> Handle(FetchCorInformation request, CancellationToken ct)
    {
        if (!await store.StartCorInformationFetchAsync(request.Owner, request.RecordId, request.Version, ct))
            return Result<CorSchedulePreview>.Failure("The uploaded COR changed. Refresh before fetching its information.", ErrorCode.Conflict);
        var preview = await PreviewAsync(request.Owner, ct);
        return preview == null || preview.RecordId != request.RecordId
            ? Result<CorSchedulePreview>.Failure("The uploaded COR changed. Refresh before fetching its information.", ErrorCode.Conflict)
            : Result<CorSchedulePreview>.Success(preview, "Information fetch requested. The COR is processed in the background.");
    }

    private async Task<CorSchedulePreview?> PreviewAsync(Guid owner, CancellationToken ct)
    {
        var record = await store.FindCurrentCorAsync(owner, ct);
        if (record == null) return null;
        var subject = await store.GetSubjectAsync(VerificationType.Cor, record.SubjectId, ct);
        if (subject == null || subject.Fingerprint != record.Fingerprint || subject.IsPersonnel) return null;
        var fields = string.IsNullOrWhiteSpace(record.ExtractedFieldsJson) ? null : JsonSerializer.Deserialize<CorFields>(record.ExtractedFieldsJson);
        var week = fields == null ? CorStudentMapping.Week(new(null, null, null, null, null) { ScheduleIssues = ["Scanning incomplete"] }) : CorStudentMapping.AuthoritativeWeek(fields);
        CorStudentMapping.ValidateWeek(week, out var intervals);
        var items = intervals.Select(s => new CorScheduleItem((int)s.Day, CorStudentMapping.Format(s.Start), CorStudentMapping.Format(s.End))).ToList();
        var processing = record.Status is VerificationState.Pending or VerificationState.Processing;
        var issues = fields?.AdminWeek != null ? [] : fields?.ScheduleIssues.ToList() ?? (processing ? [] : new List<string> { "The schedule could not be read. Upload a clearer COR." });
        if (record.Status == VerificationState.Rejected && (fields == null || CorAcademicTerm.Validate(fields).Count == 0)) issues.Add(record.Reason);
        var information = fields == null ? null : CorStudentMapping.Information(fields);
        if (information != null && !string.IsNullOrWhiteSpace(subject.Identifier))
            information = information with { FirstName = subject.FirstName, LastName = subject.LastName, StudentNumber = subject.Identifier,
                Course = subject.Course, YearLevel = subject.YearLevel, Section = CorStudentMapping.Section(subject.Section) };
        if (information != null && fields?.ConfirmedInformation is { } confirmed)
            information = information with { MiddleName = confirmed.MiddleName, College = confirmed.College };
        var account = accounts == null ? null : await accounts.GetByIdAsync(owner);
        return CorAcademicTerm.Preview(new(record.Id, record.Version, record.Status.ToString(), subject.DocumentUrl!, items, issues,
            !processing && fields != null && record.Status != VerificationState.Rejected)
            { FetchRequested = record.OcrOutcome != AwaitingInformationFetch, Week = week, StudentInformation = information, PhoneNumber = account?.PhoneNumber, InformationConfirmed = fields?.ConfirmedInformation != null,
              ScheduleReviewRequested = false,
              CanConfirmSchedule = !processing && fields != null && record.Status != VerificationState.Rejected &&
                (fields.AdminWeek != null || fields.ScheduleIssues.Count == 0) && CorStudentMapping.ValidateWeek(week, out _).Count == 0 }, fields);
    }

    public async Task<Result<bool>> Handle(ConfirmCorSchedule request, CancellationToken ct)
    {
        try
        {
            if (request.Week != null) return Result<bool>.Failure("Schedules are read-only. Upload a clearer COR for corrections.", ErrorCode.Forbidden);
            var confirmed = await store.ConfirmExtractedSchedulesAsync(request.RecordId, request.Version, request.Owner, ct);
            if (!confirmed) return Result<bool>.Failure("The COR changed or scanning is incomplete. Reload the schedule preview.", ErrorCode.Conflict);
            var record = await store.FindAsync(request.RecordId, ct);
            if (record != null) await service.EnqueueAsync(VerificationType.Schedule, record.SubjectId, ct);
            return Result<bool>.Success(true, "Schedule saved from your COR.");
        }
        catch (DocumentValidationException ex) { return Result<bool>.Failure(ex.Message, ErrorCode.BadRequest); }
    }

    public async Task<Result<bool>> Handle(ConfirmStudentFromCor request, CancellationToken ct)
    {
        var confirmation = request.Confirmation;
        var data = confirmation?.Information;
        if (confirmation == null || data == null) return Result<bool>.Failure("Review your extracted Student information first.", ErrorCode.BadRequest);
        var profile = new UpdateOnboardingProfileCommand(request.Owner, data.PhoneNumber, data.FirstName, data.LastName, data.MiddleName, null);
        var student = new UpdateOnboardingStudentCommand(request.Owner, data.StudentNumber, data.Course, data.Section, data.YearLevel);
        var errors = (await new UpdateOnboardingProfileValidator().ValidateAsync(profile, ct)).Errors
            .Concat((await new UpdateOnboardingStudentValidator().ValidateAsync(student, ct)).Errors).Select(e => e.ErrorMessage).ToList();
        if (data.FirstName?.Length > 150 || data.LastName?.Length > 150 || data.MiddleName?.Length > 150 ||
            data.Course?.Length > 150 || data.Section?.Length > 50 || data.College?.Length > 100 ||
            !System.Text.RegularExpressions.Regex.IsMatch(data.PhoneNumber ?? "", @"^09\d{9}$"))
            errors.Add("Check your field lengths and enter an 11-digit phone number starting with 09.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(data.AcademicYear ?? "", @"^\d{4}-\d{4}$") ||
            !int.TryParse((data.AcademicYear ?? "").Split('-').FirstOrDefault(), out var year) ||
            !int.TryParse((data.AcademicYear ?? "").Split('-').LastOrDefault(), out var end) || end != year + 1 ||
            CorTextVerification.SemesterKey(data.Semester) is not ("1" or "2" or "SUMMER"))
            errors.Add("Complete the academic year and semester from your COR.");
        if (errors.Count > 0) return Result<bool>.Failure(string.Join(" ", errors.Distinct()), ErrorCode.BadRequest);
        var normalized = data with { FirstName = data.FirstName!.Trim(), LastName = data.LastName!.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(data.MiddleName) ? null : data.MiddleName.Trim(),
            StudentNumber = Student.NormalizeNumber(data.StudentNumber), Course = data.Course?.Trim(), Section = CorStudentMapping.Section(data.Section)!, College = data.College?.Trim() };
        try
        {
            if (!await store.ConfirmStudentFromCorAsync(request.Owner, confirmation with { Information = normalized }, ct))
                return Result<bool>.Failure("The COR changed or scanning is incomplete. Reload before confirming.", ErrorCode.Conflict);
            await service.RefreshStudentRegistrationAsync(request.Owner, ct);
            return Result<bool>.Success(true, "Student details and weekly schedule saved. Continue to vehicle registration.");
        }
        catch (DocumentValidationException ex) { return Result<bool>.Failure(ex.Message, ErrorCode.BadRequest); }
    }

    public Task<Result<bool>> Handle(RequestCorScheduleReview request, CancellationToken ct) =>
        Task.FromResult(Result<bool>.Failure("Schedules no longer require admin review. Upload a clearer COR if the extraction is incorrect.", ErrorCode.Forbidden));

    public Task<Result<bool>> Handle(CorrectCorSchedule request, CancellationToken ct) =>
        Task.FromResult(Result<bool>.Failure("Schedules are generated from the COR and no longer use admin review or correction.", ErrorCode.Forbidden));

    public async Task<VerificationPolicy> Handle(GetVerificationPolicy request, CancellationToken ct)
    {
        var policy = await store.GetPolicyAsync(ct);
        policy.VehicleAutomaticApproval = false;
        return policy;
    }
    public async Task<Result<bool>> Handle(UpdateVerificationPolicy request, CancellationToken ct)
    {
        var p = request.Policy;
        p.VehicleAutomaticApproval = false;
        if (request.Actor == Guid.Empty || p.Version < 0 ||
            !System.Text.RegularExpressions.Regex.IsMatch(p.AcademicYear, @"^\d{4}-\d{4}$") ||
            !int.TryParse(p.AcademicYear[..4], out var year) || !int.TryParse(p.AcademicYear[5..], out var end) || end != year + 1 ||
            CorTextVerification.SemesterKey(p.Semester) is not ("1" or "2" or "SUMMER"))
            return Result<bool>.Failure("Enter an academic year (YYYY-YYYY) and semester (1st, 2nd or Summer).", ErrorCode.BadRequest);
        return await store.UpdatePolicyAsync(p, request.Actor, ct) ? Result<bool>.Success(true) : Result<bool>.Failure("Settings changed. Reload before saving.", ErrorCode.Conflict);
    }
    public async Task<List<VerificationDto>> Handle(ListVerifications request, CancellationToken ct) =>
        (await store.ListAsync(request.UserId, request.History, ct)).Where(r => r.Type != VerificationType.Schedule).Select(r => new VerificationDto(r.Id, r.UserAccountId, r.SubjectId,
            r.Type.ToString(), r.Status.ToString(), r.Method?.ToString(), r.Reason, r.OcrOutcome,
            request.IncludeExtractedFields ? r.ExtractedFieldsJson : null, r.VerifiedAt, r.ReviewerId, r.Attempts, r.Version, r.IsCurrent, r.CreatedAt)).ToList();
    public Task<List<VerificationAudit>> Handle(ListVerificationAudit request, CancellationToken ct) => store.AuditAsync(request.RecordId, ct);
    public async Task<Result<bool>> Handle(ReviewVerification request, CancellationToken ct)
    {
        if (!Enum.TryParse<VerificationState>(request.Status, out var status)) return Result<bool>.Failure("Invalid review status.", ErrorCode.BadRequest);
        try { return ReviewResult(await service.ReviewAsync(request.Id, request.Version, status, request.Reason, request.Actor, ct)); }
        catch (DocumentValidationException ex) { return Result<bool>.Failure(ex.Message, ErrorCode.BadRequest); }
    }
    public async Task<Result<bool>> Handle(ReviewVerificationSubject request, CancellationToken ct)
    {
        if (!Enum.TryParse<VerificationType>(request.Type, out var type) || !Enum.IsDefined(type) ||
            !Enum.TryParse<VerificationState>(request.Status, out var status)) return Result<bool>.Failure("Invalid verification type or status.", ErrorCode.BadRequest);
        try { return ReviewResult(await service.ReviewSubjectAsync(type, request.SubjectId, status, request.Reason, request.Actor, ct)); }
        catch (DocumentValidationException ex) { return Result<bool>.Failure(ex.Message, ErrorCode.BadRequest); }
    }
    private static Result<bool> ReviewResult(bool success) => success ? Result<bool>.Success(true, "Verification decision saved.")
        : Result<bool>.Failure("Submission changed or was not found. Reload before reviewing.", ErrorCode.Conflict);
}
