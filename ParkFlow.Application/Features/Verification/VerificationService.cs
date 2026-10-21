using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Verification;

public class VerificationService(IVerificationStore store, ICorDocumentReader reader,
    ICloudinaryService cloudinary, IInstitutionalVerificationProvider trusted,
    ISignalRNotificationSender signalR, INotificationService notifications, ICacheService cache, IEmailService? emails = null,
    ILogger<VerificationService>? logger = null)
{
    public async Task EnqueueAsync(VerificationType type, Guid id, CancellationToken ct)
    {
        if (type == VerificationType.Vehicle) return;
        var record = await store.EnqueueAsync(type, id, ct);
        if (record != null && type != VerificationType.Schedule) await NotifyAsync(record, ct, false);
    }

    public static List<string> MandatoryChecks(VerificationSubject subject, VerificationPolicy policy)
    {
        var issues = new List<string>();
        if (subject.Type == VerificationType.Account)
        {
            if (subject.Suspended) issues.Add("Suspended accounts require an account administrator.");
            if (!subject.EmailVerified) issues.Add("Verify your email using the email verification code.");
            if (string.IsNullOrWhiteSpace(subject.FirstName) || string.IsNullOrWhiteSpace(subject.LastName)) issues.Add("Complete your profile.");
            var idIsValid = subject.IsPersonnel
                ? Regex.IsMatch(subject.Identifier ?? "", @"^[A-Z0-9][A-Z0-9\- ]{3,29}$", RegexOptions.IgnoreCase)
                : Regex.IsMatch(CorTextVerification.Compact(subject.Identifier), @"^[0-9]{10}$");
            if (!idIsValid)
                issues.Add("A valid student or employee identification number is required.");
            if (subject.DuplicateIdentifier) issues.Add("The institutional identification number is already registered.");
        }
        if (subject.Type == VerificationType.Cor && string.IsNullOrWhiteSpace(subject.DocumentUrl))
            issues.Add("Upload a COR or employee identity document.");
        if (subject.Type == VerificationType.Vehicle)
        {
            if (!Regex.IsMatch(CorTextVerification.Compact(subject.PlateNumber), @"^[A-Z0-9]{4,10}$")) issues.Add("Enter a valid plate number.");
            if (string.IsNullOrWhiteSpace(subject.Brand)) issues.Add("Vehicle brand is required.");
            if (subject.DuplicateIdentifier) issues.Add("The plate number is already registered.");
            if (!subject.OwnershipDocumentsPresent) issues.Add("Upload the OR/CR and a vehicle picture for ownership review.");
        }
        if (subject.Type == VerificationType.Schedule)
        {
            issues.AddRange(CorTextVerification.CheckSchedules(subject.Schedules));
            if (!subject.CorApproved) issues.Add("COR approval is required before approving schedules.");
        }
        return issues;
    }

    public async Task ProcessAsync(VerificationRecord record, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        try { await ProcessRecordAsync(record, ct); }
        finally { logger?.LogInformation("Verification processing {Type} in {DurationMs} ms", record.Type, watch.Elapsed.TotalMilliseconds); }
    }

    public async Task ProcessPreparedCorAsync(Guid owner, Guid subjectId, string documentUrl, CancellationToken ct)
    {
        // Use the existing exclusive database lease and approval checks. Native
        // OCR never runs inline, including if this source changes concurrently.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var claim = await store.ClaimCorAsync(subjectId, TimeSpan.FromSeconds(30), timeout.Token);
        if (claim == null) return;
        try
        {
            if (claim.UserAccountId != owner || claim.ExtractedFieldsJson == null || claim.OcrOutcome != "DigitalPdfTextExtracted" ||
                JsonSerializer.Deserialize<CorFields>(claim.ExtractedFieldsJson)?.SourceDocumentUrl != documentUrl)
                throw new IOException("Prepared digital extraction changed.");
            await ProcessRecordAsync(claim, timeout.Token, preparedOnly: true);
        }
        catch
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await store.DeferPreparedCorAsync(claim, cleanup.Token);
            throw;
        }
    }

    private async Task ProcessRecordAsync(VerificationRecord record, CancellationToken ct, bool preparedOnly = false)
    {
        if (record.Type == VerificationType.Vehicle) return;
        var subject = await store.GetSubjectAsync(record.Type, record.SubjectId, ct);
        if (subject == null) { await store.RetireAsync(record, ct); return; }
        if (subject.Fingerprint != record.Fingerprint) { await EnqueueAsync(record.Type, record.SubjectId, ct); return; }
        var policy = await store.GetPolicyAsync(ct);
        var issues = MandatoryChecks(subject, policy);
        string? outcome = null, fieldsJson = null;
        if (record.Type == VerificationType.Cor && issues.Count == 0)
        {
            if (!reader.IsStoredDocument(subject.DocumentUrl!))
                issues.Add("Upload the document using ParkFlow's document upload.");
            else
            {
                var currentFields = subject.IsPersonnel || record.ExtractedFieldsJson == null ? null : JsonSerializer.Deserialize<CorFields>(record.ExtractedFieldsJson);
                var previous = currentFields?.SourceDocumentUrl == subject.DocumentUrl ? record :
                    subject.IsPersonnel ? null : await store.FindCorExtractionByDocumentAsync(subject.UserId, subject.DocumentUrl!, ct);
                var cachedFields = previous == record ? currentFields : previous?.ExtractedFieldsJson == null ? null : JsonSerializer.Deserialize<CorFields>(previous.ExtractedFieldsJson);
                if (cachedFields != null)
                {
                    outcome = previous!.OcrOutcome ?? "DocumentExtractionReused";
                    fieldsJson = previous!.ExtractedFieldsJson;
                    issues.AddRange(CorTextVerification.Compare(cachedFields, subject, policy));
                }
                else
                {
                    if (preparedOnly) throw new IOException("Native OCR requires background processing.");
                    var fetchWatch = Stopwatch.StartNew();
                    var document = await cloudinary.GetDocumentStreamAsync(subject.DocumentUrl!);
                    logger?.LogInformation("COR stored file fetch in {DurationMs} ms", fetchWatch.Elapsed.TotalMilliseconds);
                    if (document == null) throw new IOException("Document storage unavailable.");
                    await using var stream = document.Value.Stream;
                    try
                    {
                        var result = await reader.ReadAsync(stream, DocumentFileName(subject.DocumentUrl!, document.Value.ContentType), ct);
                        outcome = result.Outcome;
                        if (string.IsNullOrWhiteSpace(result.Text)) issues.Add("Document is unreadable. Upload a clearer image or PDF.");
                        else if (subject.IsPersonnel) issues.Add("Employee identity documents require institutional review.");
                        else
                        {
                            var parseWatch = Stopwatch.StartNew();
                            var fields = CorTextVerification.Extract(result.Text) with { SourceDocumentUrl = subject.DocumentUrl };
                            logger?.LogInformation("COR field and schedule parsing in {DurationMs} ms; sessions {SessionCount}, issues {IssueCount}",
                                parseWatch.Elapsed.TotalMilliseconds, fields.Schedules.Count, fields.ScheduleIssues.Count);
                            fieldsJson = JsonSerializer.Serialize(fields);
                            issues.AddRange(CorTextVerification.Compare(fields, subject, policy));
                        }
                    }
                    catch (DocumentValidationException ex) { issues.Add(ex.Message); outcome = "InvalidDocument"; }
                }
            }
        }
        var evidence = issues.Count == 0 && record.Type != VerificationType.Schedule && subject.IsPersonnel
            ? await trusted.VerifyAsync(subject, ct) : new TrustedVerificationEvidence(false, null, subject.Fingerprint);
        var authentic = evidence.Succeeded && !string.IsNullOrWhiteSpace(evidence.Reference) && evidence.Fingerprint == subject.Fingerprint;
        var decision = Decide(record.Type, policy.Allows(record.Type), issues, authentic, outcome, fieldsJson) with { PolicyVersion = policy.Version };
        if (record.Type == VerificationType.Cor && !subject.IsPersonnel)
        {
            var extracted = fieldsJson == null ? null : JsonSerializer.Deserialize<CorFields>(fieldsJson);
            var termErrors = extracted == null ? new List<string>() : CorAcademicTerm.Validate(extracted);
            var ready = extracted != null && (extracted.AdminWeek != null || extracted.ScheduleIssues.Count == 0) &&
                CorStudentMapping.ValidateWeek(CorStudentMapping.AuthoritativeWeek(extracted), out _).Count == 0;
            decision = new(ready ? extracted!.ConfirmedInformation != null ? VerificationState.Approved : VerificationState.PreVerified : VerificationState.NeedsReview,
                ready ? extracted!.ConfirmedInformation != null ? "COR information and daily schedule confirmed; no admin approval required." : "COR ready. Review your information and confirm the schedule."
                    : "The COR schedule could not be read completely. Upload a clearer COR.", outcome, fieldsJson) { PolicyVersion = policy.Version };
            if (termErrors.Count > 0)
                decision = new(VerificationState.Rejected, string.Join(" ", termErrors), "AcademicTermMismatch", fieldsJson) { PolicyVersion = policy.Version };
        }
        if (await store.CompleteAsync(record, decision, ct))
        {
            var committed = await store.FindAsync(record.Id, ct);
            if (committed == null) return;
            await NotifyAsync(committed, ct, true);
            if (record.Type == VerificationType.Cor && committed.Status == VerificationState.Approved && !subject.IsPersonnel)
                await EnqueueAsync(VerificationType.Schedule, record.SubjectId, ct);
        }
    }

    public static VerificationDecision Decide(VerificationType type, bool enabled, List<string> issues, bool authentic,
        string? outcome = null, string? fields = null)
    {
        if (type == VerificationType.Vehicle)
            return new(VerificationState.NeedsReview, issues.Count > 0 ? string.Join(" ", issues) : "OR/CR and plate photos require manual vehicle review.");
        if (issues.Count > 0) return new(VerificationState.NeedsReview, string.Join(" ", issues), outcome, fields);
        if (enabled && (type == VerificationType.Schedule || authentic))
            return new(VerificationState.Approved, type == VerificationType.Schedule
                ? "University schedule rules passed." : "Mandatory checks and trusted institutional verification succeeded.", outcome, fields, authentic);
        return new(VerificationState.PreVerified, type == VerificationType.Schedule
            ? "Schedule rules passed. Automatic approval is disabled; admin review is required."
            : "Information checks passed. Admin review is required to verify identity, authenticity or ownership.", outcome, fields);
    }

    public async Task<bool> ReviewAsync(Guid id, int version, VerificationState status, string reason, Guid actor, CancellationToken ct)
    {
        if (actor == Guid.Empty || status is not (VerificationState.Approved or VerificationState.Rejected) || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            throw new DocumentValidationException("Choose approve or reject and provide a review reason (up to 1000 characters).");
        var record = await store.FindAsync(id, ct);
        if (record == null) return false;
        if (record.Type == VerificationType.Schedule)
            throw new DocumentValidationException("Schedules no longer require admin approval.");
        if (record.Type == VerificationType.Vehicle)
            throw new DocumentValidationException("Vehicle documents use manual review from Vehicle Approvals.");
        var subject = await store.GetSubjectAsync(record.Type, record.SubjectId, ct);
        if (subject == null) return false;
        if (!subject.IsPersonnel && record.Type is VerificationType.Account or VerificationType.Cor)
            throw new DocumentValidationException("Student registration and COR schedules do not require admin approval. Review the vehicle documents instead.");
        if (status == VerificationState.Approved)
        {
            var issues = MandatoryChecks(subject, await store.GetPolicyAsync(ct));
            if (issues.Count > 0) throw new DocumentValidationException(string.Join(" ", issues));
        }
        if (!await store.ReviewAsync(id, version, subject.Fingerprint, new(status, reason.Trim()), actor, ct)) return false;
        record.Status = status; record.Reason = reason;
        await NotifyAsync(record, ct, true);
        if (record.Type == VerificationType.Cor && status == VerificationState.Approved && !subject.IsPersonnel)
            await EnqueueAsync(VerificationType.Schedule, record.SubjectId, ct);
        return true;
    }

    public async Task<bool> ReviewSubjectAsync(VerificationType type, Guid subjectId, VerificationState status, string reason, Guid actor, CancellationToken ct)
    {
        if (type == VerificationType.Vehicle)
        {
            if (actor == Guid.Empty || status is not (VerificationState.Approved or VerificationState.Rejected) || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
                throw new DocumentValidationException("Choose approve or reject and provide a review reason.");
            var subject = await store.GetSubjectAsync(type, subjectId, ct);
            if (subject == null) return false;
            if (status == VerificationState.Approved)
            {
                var issues = MandatoryChecks(subject, await store.GetPolicyAsync(ct));
                if (issues.Count > 0) throw new DocumentValidationException(string.Join(" ", issues));
            }
            if (!await store.ReviewVehicleManuallyAsync(subjectId, subject.Fingerprint, status, reason.Trim(), ct)) return false;
            await cache.RemoveByPrefixAsync(ParkFlow.Application.Common.CacheKeys.VehiclePrefix, ct);
            await cache.RemoveByPrefixAsync(ParkFlow.Application.Common.CacheKeys.UserPrefix, ct);
            await cache.RemoveByPrefixAsync(ParkFlow.Application.Common.CacheKeys.DashboardPrefix, ct);
            try
            {
                await signalR.SendToUserAsync(subject.UserId.ToString(), "VerificationStatusChanged", new { type = "Vehicle", status = status.ToString(), reason, vehicleId = subjectId });
                await signalR.SendToAllAsync("ApprovalListUpdated", new { type = "Vehicle", vehicleId = subjectId });
                await notifications.CreateAndSendNotificationAsync(subject.UserId, "Vehicle review updated", reason,
                    type: "verification_updated", actionRoute: "/(settings)/vehicle");
                if (emails != null && !string.IsNullOrWhiteSpace(subject.Email))
                    await emails.SendEmailAsync(subject.Email, $"ParkFlow - Vehicle review {status}",
                        $"<p>Your vehicle review is {status}.</p><p>{System.Net.WebUtility.HtmlEncode(reason)}</p>");
            }
            catch { /* The committed manual status remains available if notifications fail. */ }
            return true;
        }
        var record = await store.EnqueueAsync(type, subjectId, ct);
        return record != null && await ReviewAsync(record.Id, record.Version, status, reason, actor, ct);
    }

    private static string DocumentFileName(string url, string contentType)
    {
        var extension = Path.GetExtension(Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url);
        return "document" + (extension is ".pdf" or ".jpg" or ".jpeg" or ".png" ? extension : contentType switch
        { "image/png" => ".png", "image/jpeg" => ".jpg", _ => ".pdf" });
    }

    private async Task NotifyAsync(VerificationRecord record, CancellationToken ct, bool persist)
    {
        if (record.Type == VerificationType.Schedule) return;
        if (record.Type is VerificationType.Account or VerificationType.Cor &&
            (await store.GetSubjectAsync(record.Type, record.SubjectId, ct))?.IsPersonnel != true) return;
        await cache.RemoveByPrefixAsync(ParkFlow.Application.Common.CacheKeys.UserPrefix, ct);
        await cache.RemoveByPrefixAsync(ParkFlow.Application.Common.CacheKeys.SchedulePrefix, ct);
        await cache.RemoveByPrefixAsync(ParkFlow.Application.Common.CacheKeys.VehiclePrefix, ct);
        await cache.RemoveByPrefixAsync(ParkFlow.Application.Common.CacheKeys.DashboardPrefix, ct);
        // Only non-sensitive identifiers and states are broadcast to dashboards.
        try
        {
            await signalR.SendToUserAsync(record.UserAccountId.ToString(), "VerificationStatusChanged", new
            { type = record.Type.ToString(), status = record.Status.ToString(), reason = record.Reason, recordId = record.Id });
            await signalR.SendToAllAsync("ApprovalListUpdated", new { type = record.Type.ToString(), recordId = record.Id });
            if (persist) await notifications.CreateAndSendNotificationAsync(record.UserAccountId, $"{record.Type} verification updated",
                record.Reason, type: "verification_updated", actionRoute: record.Type == VerificationType.Vehicle ? "/(settings)/vehicle" : "/(settings)/setup-schedule");
            if (persist && emails != null && record.Status is VerificationState.Approved or VerificationState.Rejected)
            {
                var subject = await store.GetSubjectAsync(record.Type, record.SubjectId, ct);
                if (!string.IsNullOrWhiteSpace(subject?.Email))
                    await emails.SendEmailAsync(subject.Email, $"ParkFlow - {record.Type} verification {record.Status}",
                        $"<p>Your {record.Type} verification is {record.Status}.</p><p>{System.Net.WebUtility.HtmlEncode(record.Reason)}</p><p>Open ParkFlow to view your verification status.</p>");
            }
        }
        catch { /* Polling remains available if notification transport is unavailable. */ }
    }

    public Task PublishAsync(VerificationRecord record, CancellationToken ct, bool persist = false) => NotifyAsync(record, ct, persist);

    public async Task RefreshStudentRegistrationAsync(Guid owner, CancellationToken ct)
    {
        await cache.RemoveByPrefixAsync(ParkFlow.Application.Common.CacheKeys.UserPrefix, ct);
        await cache.RemoveByPrefixAsync(ParkFlow.Application.Common.CacheKeys.SchedulePrefix, ct);
        await cache.RemoveByPrefixAsync(ParkFlow.Application.Common.CacheKeys.DashboardPrefix, ct);
    }
}
