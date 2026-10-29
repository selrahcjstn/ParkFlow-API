using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.Verification;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Persistence.Repositories;

public class VerificationStore(AppDbContext db) : IVerificationStore
{
    public Task<AccountStatus?> RestoreAccountAsync(Guid id, CancellationToken ct) => Transaction<AccountStatus?>(async () =>
    {
        await Lock(VerificationType.Account, id, ct);
        var account = await db.UserAccounts.Include(u => u.UserProfile).ThenInclude(p => p!.Admin)
            .Include(u => u.UserProfile).ThenInclude(p => p!.Guard).SingleOrDefaultAsync(u => u.Id == id, ct);
        if (account == null) return null;
        if (account.Status != AccountStatus.Suspended) return account.Status;

        // Clear suspension without approving a new or changed document revision.
        account.UpdateStatus(AccountStatus.PendingVerification);
        await db.SaveChangesAsync(ct);
        if (account.UserProfile?.Admin != null || account.UserProfile?.Guard != null)
        {
            // These roles are verified when explicitly provisioned by an administrator.
            account.UpdateStatus(AccountStatus.Active);
        }
        else
        {
            var record = await EnqueueAsync(VerificationType.Account, id, ct);
            // Student status is recalculated by EnqueueAsync. Personnel retain only
            // an approval for the unchanged, current identity revision.
            if (record?.Status == VerificationState.Approved) await Apply(record, ct);
        }
        await db.SaveChangesAsync(ct);
        return account.Status;
    }, ct);

    public Task<VerificationRecord?> FindCurrentCorAsync(Guid owner, CancellationToken ct) => db.VerificationRecords.AsNoTracking()
        .Where(r => r.UserAccountId == owner && r.Type == VerificationType.Cor && r.IsCurrent).OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync(ct);

    public Task DeferPreparedCorAsync(VerificationRecord claim, CancellationToken ct) => Transaction(async () =>
    {
        await Lock(VerificationType.Cor, claim.SubjectId, ct);
        var record = await db.VerificationRecords.SingleOrDefaultAsync(r => r.Id == claim.Id, ct);
        if (record == null || !record.IsCurrent || record.Status != VerificationState.Processing || record.LeaseId != claim.LeaseId) return false;
        record.Status = VerificationState.Pending; record.LeaseId = null; record.LeaseExpiresAt = null;
        record.Attempts = Math.Max(0, record.Attempts - 1); record.NextAttemptAt = DateTime.UtcNow; record.Version++;
        record.Reason = "Digital extraction is saved; background verification will continue.";
        Audit(record.Id, null, "DigitalProcessingDeferred", "Inline processing deferred without consuming a background OCR attempt.");
        await Apply(record, ct); await db.SaveChangesAsync(ct); return true;
    }, ct);
    public async Task<VerificationRecord?> FindCorExtractionByDocumentAsync(Guid owner, string documentUrl, CancellationToken ct)
    {
        var candidates = await db.VerificationRecords.AsNoTracking().Where(r => r.UserAccountId == owner && r.Type == VerificationType.Cor && r.ExtractedFieldsJson != null)
            .OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync(ct);
        return candidates.FirstOrDefault(r => JsonSerializer.Deserialize<CorFields>(r.ExtractedFieldsJson!)?.SourceDocumentUrl == documentUrl);
    }

    public Task<bool> StartCorInformationFetchAsync(Guid owner, Guid recordId, int version, CancellationToken ct) => Transaction(async () =>
    {
        var initial = await db.VerificationRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == recordId && r.UserAccountId == owner && r.Type == VerificationType.Cor, ct);
        if (initial == null) return false;
        await Lock(VerificationType.Cor, initial.SubjectId, ct);
        var record = await db.VerificationRecords.SingleAsync(r => r.Id == recordId, ct);
        await db.Entry(record).ReloadAsync(ct);
        var source = await GetSubjectAsync(VerificationType.Cor, record.SubjectId, ct);
        if (!record.IsCurrent || source == null || source.IsPersonnel || source.Fingerprint != record.Fingerprint) return false;
        // Repeated clicks or a lost HTTP response reuse the same durable job/result.
        if (record.OcrOutcome != VerificationRequests.AwaitingInformationFetch) return true;
        if (record.Version != version) return false;
        record.OcrOutcome = "InformationFetchRequested";
        record.NextAttemptAt = DateTime.UtcNow; record.Version++; record.UpdatedAt = DateTime.UtcNow;
        record.Reason = "Information requested. Reading the COR in the background.";
        await db.SaveChangesAsync(ct); return true;
    }, ct);

    public Task<bool> ConfirmStudentFromCorAsync(Guid owner, CorStudentConfirmation confirmation, CancellationToken ct) => Transaction(async () =>
    {
        await Lock(VerificationType.Account, owner, ct);
        var record = await db.VerificationRecords.FirstOrDefaultAsync(r => r.Id == confirmation.RecordId && r.UserAccountId == owner && r.Type == VerificationType.Cor, ct);
        if (record == null) return false;
        await Lock(VerificationType.Schedule, record.SubjectId, ct);
        await db.Entry(record).ReloadAsync(ct);
        var source = await GetSubjectAsync(VerificationType.Cor, record.SubjectId, ct);
        var user = await db.UserAccounts.Include(u => u.AuthIdentities).FirstOrDefaultAsync(u => u.Id == owner, ct);
        var profile = await db.UserProfiles.Include(p => p.Student).Include(p => p.Personnel).Include(p => p.Admin).Include(p => p.Guard)
            .FirstOrDefaultAsync(p => p.UserAccountId == owner, ct);
        if (source == null || user == null || profile?.Personnel != null || profile?.Admin != null || profile?.Guard != null)
            throw new DocumentValidationException("This COR registration step is only available to Student accounts.");
        if (!user.AuthIdentities.Any(i => i.IsVerified)) throw new DocumentValidationException("Verify your email before confirming Student registration.");
        var data = confirmation.Information;
        var oldFields = string.IsNullOrWhiteSpace(record.ExtractedFieldsJson) ? null : JsonSerializer.Deserialize<CorFields>(record.ExtractedFieldsJson);
        if (oldFields == null) return false;
        var termErrors = CorAcademicTerm.Validate(oldFields, data.AcademicYear, data.Semester);
        if (termErrors.Count > 0) throw new DocumentValidationException(string.Join(" ", termErrors));
        var intervals = CorStudentMapping.ConfirmableSchedules(oldFields, confirmation.Week);
        var existingIntervals = await db.ParkingSchedules.Where(s => s.SubmissionId == record.SubjectId).ToListAsync(ct);
        var intervalSnapshot = existingIntervals.OrderBy(s => s.DayOfWeek).Select(s => new VerificationSchedule(s.DayOfWeek, s.StartTime, s.EndTime));
        // A retry after a committed response was lost returns success without writing
        // again. A different document or corrected profile cannot replay the receipt.
        var alreadySaved = oldFields?.SourceDocumentUrl == source.DocumentUrl && oldFields?.ConfirmedInformation == data && profile?.Student != null &&
            profile.FirstName == data.FirstName && profile.LastName == data.LastName && profile.MiddleName == data.MiddleName &&
            user.PhoneNumber == data.PhoneNumber && profile.Student.StudentNumber == Student.NormalizeNumber(data.StudentNumber) &&
            profile.Student.Course == data.Course && profile.Student.Section == data.Section && profile.Student.YearLevel == data.YearLevel &&
            source.AcademicTerm == data.AcademicYear && intervalSnapshot.SequenceEqual(intervals.OrderBy(s => s.Day));
        if (alreadySaved) return true;
        if (user.OnboardingStep == OnboardingStep.Done)
            throw new DocumentValidationException("Use your existing profile settings to update a completed registration.");
        if (await db.Set<ParkingLog>().AnyAsync(p => p.Vehicle.OwnerId == owner && p.Status == ParkingStatus.Parked && p.ExitTime == null, ct))
            throw new DocumentValidationException("Exit the parking facility before updating your schedule.");
        if (!record.IsCurrent || record.Version != confirmation.Version || record.Fingerprint != source.Fingerprint ||
            record.Status is VerificationState.Pending or VerificationState.Processing or VerificationState.Rejected || oldFields == null)
            return false;
        var number = Student.NormalizeNumber(data.StudentNumber);
        var profileId = profile?.Id ?? Guid.Empty;
        if (await db.Students.AnyAsync(s => s.StudentNumber == number && s.UserProfileId != profileId, ct))
            throw new DocumentValidationException("This student ID number is already registered.");
        if (profile == null)
        {
            profile = new UserProfile(owner, data.FirstName, data.LastName, data.MiddleName, null);
            db.UserProfiles.Add(profile);
        }
        else profile.UpdateProfile(data.FirstName, data.LastName, data.MiddleName, null);
        if (profile.Student == null) db.Students.Add(new Student(profile.Id, number, data.Course, data.Section, data.YearLevel));
        else profile.Student.UpdateDetails(number, data.Course, data.Section, data.YearLevel);
        user.UpdatePhoneNumber(data.PhoneNumber); user.UpdateOnboardingStep(OnboardingStep.Vehicle);
        var cor = await db.CorSubmissions.FindAsync([record.SubjectId], ct);
        cor!.UpdateSubmission(data.AcademicYear, null, null);
        db.ParkingSchedules.RemoveRange(existingIntervals);
        db.ParkingSchedules.AddRange(intervals.Select(s => new ParkingSchedule(record.SubjectId, s.Day, s.Start, s.End)));
        record.ExtractedFieldsJson = JsonSerializer.Serialize(oldFields with { ConfirmedCollege = data.College, SourceDocumentUrl = source.DocumentUrl,
            ConfirmedInformation = data, ConfirmedWeek = CorStudentMapping.AuthoritativeWeek(oldFields) });
        record.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { throw new DocumentValidationException("This student ID number is already registered."); }
        await EnqueueAsync(VerificationType.Account, owner, ct);
        var currentCor = await EnqueueAsync(VerificationType.Cor, record.SubjectId, ct);
        if (currentCor != null)
        {
            currentCor.ExtractedFieldsJson = record.ExtractedFieldsJson; currentCor.OcrOutcome = record.OcrOutcome;
            SetDecision(currentCor, new(VerificationState.Approved, "COR information and daily schedule confirmed by the student; no admin approval required."), VerificationMethod.Automatic, null);
            await Apply(currentCor, ct);
        }
        await EnqueueAsync(VerificationType.Schedule, record.SubjectId, ct);
        Audit(record.Id, owner, "StudentInformationConfirmed", "Student details were confirmed from this COR.");
        await db.SaveChangesAsync(ct);
        return true;
    }, ct);

    private const string RemovedDailyLimitReason = "Daily schedule exceeds the university parking limit.";

    internal Task<int> RetireVehicleJobsAsync(Guid id, CancellationToken ct) => db.VerificationRecords
        .Where(r => r.Type == VerificationType.Vehicle && r.SubjectId == id && r.IsCurrent)
        .ExecuteUpdateAsync(update => update.SetProperty(r => r.IsCurrent, false).SetProperty(r => r.LeaseId, (Guid?)null)
            .SetProperty(r => r.LeaseExpiresAt, (DateTime?)null).SetProperty(r => r.Version, r => r.Version + 1), ct);

    private async Task RetireScheduleJobsAsync(Guid id, CancellationToken ct)
    {
        await db.VerificationRecords.Where(r => r.Type == VerificationType.Schedule && r.SubjectId == id && r.IsCurrent)
            .ExecuteUpdateAsync(update => update.SetProperty(r => r.IsCurrent, false).SetProperty(r => r.LeaseId, (Guid?)null)
                .SetProperty(r => r.LeaseExpiresAt, (DateTime?)null).SetProperty(r => r.Version, r => r.Version + 1), ct);
        var cor = await db.CorSubmissions.FindAsync([id], ct);
        if (cor != null) { cor.UpdateScheduleVerification(CorVerificationStatus.Verified); await db.SaveChangesAsync(ct); }
    }

    public Task<bool> ReviewVehicleManuallyAsync(Guid id, string fingerprint, VerificationState status, string reason, Guid actor, CancellationToken ct) => Transaction(async () =>
    {
        if (status is not (VerificationState.Approved or VerificationState.Rejected) || string.IsNullOrWhiteSpace(reason)) return false;
        var owner = await db.Vehicles.Where(v => v.Id == id).Select(v => (Guid?)v.OwnerId).SingleOrDefaultAsync(ct);
        if (owner == null) return false;
        await Lock(VerificationType.Account, owner.Value, ct);
        await Lock(VerificationType.Vehicle, id, ct);
        var subject = await GetSubjectAsync(VerificationType.Vehicle, id, ct);
        if (subject == null || subject.Fingerprint != fingerprint) return false;
        if (status == VerificationState.Approved && VerificationService.MandatoryChecks(subject, await GetPolicyAsync(ct)).Count > 0) return false;
        await RetireVehicleJobsAsync(id, ct);
        var vehicle = await db.Vehicles.FindAsync([id], ct);
        if (vehicle == null) return false;
        await db.Entry(vehicle).ReloadAsync(ct);
        var driver = await db.UserProfiles.SingleAsync(p => p.UserAccountId == owner.Value, ct);
        if (status == VerificationState.Approved && (string.IsNullOrWhiteSpace(driver.DriversLicenseUrl) || string.IsNullOrWhiteSpace(driver.ProfilePictureUrl))) return false;
        if (status == VerificationState.Approved || driver.LicenseStatus != CorVerificationStatus.Verified)
            driver.ReviewLicense(status == VerificationState.Approved, actor, reason);
        vehicle.UpdateVerificationStatus(status == VerificationState.Approved ? CorVerificationStatus.Verified : CorVerificationStatus.Rejected, reason);
        await db.SaveChangesAsync(ct);
        if (await db.UserProfiles.AnyAsync(p => p.UserAccountId == owner.Value && (p.Student != null || p.Personnel != null), ct))
            await EnqueueAsync(VerificationType.Account, owner.Value, ct);
        return true;
    }, ct);

    public Task<bool> ConfirmExtractedSchedulesAsync(Guid recordId, int version, Guid owner, CancellationToken ct, List<VerificationSchedule>? corrected = null) => Transaction(async () =>
    {
        var initial = await db.VerificationRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == recordId && x.UserAccountId == owner && x.Type == VerificationType.Cor, ct);
        if (initial == null) return false;
        await Lock(VerificationType.Schedule, initial.SubjectId, ct);
        var record = await db.VerificationRecords.SingleAsync(x => x.Id == recordId, ct);
        var subject = await GetSubjectAsync(VerificationType.Cor, record.SubjectId, ct);
        if (!record.IsCurrent || record.Version != version || subject?.Fingerprint != record.Fingerprint ||
            record.Status is VerificationState.Pending or VerificationState.Processing or VerificationState.Rejected || subject.IsPersonnel)
            return false;
        if (await db.Set<ParkingLog>().AnyAsync(p => p.Vehicle.OwnerId == owner && p.Status == ParkingStatus.Parked && p.ExitTime == null, ct))
            throw new DocumentValidationException("Exit the parking facility before updating your schedule.");
        var fields = string.IsNullOrWhiteSpace(record.ExtractedFieldsJson) ? null : JsonSerializer.Deserialize<CorFields>(record.ExtractedFieldsJson);
        if (corrected != null) throw new DocumentValidationException("Schedules are read-only. Request an admin review for corrections.");
        if (fields == null) throw new DocumentValidationException("The schedule has not been extracted yet.");
        var termErrors = CorAcademicTerm.Validate(fields);
        if (termErrors.Count > 0) throw new DocumentValidationException(string.Join(" ", termErrors));
        var intervals = CorStudentMapping.ConfirmableSchedules(fields);
        SetDecision(record, new(VerificationState.Approved, "COR schedule confirmed by the student; no admin approval required."), VerificationMethod.Automatic, null);
        await Apply(record, ct);
        await EnqueueAsync(VerificationType.Account, owner, ct);
        await EnqueueAsync(VerificationType.Schedule, record.SubjectId, ct);
        var existing = await db.ParkingSchedules.Where(x => x.SubmissionId == record.SubjectId).ToListAsync(ct);
        var ordered = existing.OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime).Select(x => new VerificationSchedule(x.DayOfWeek, x.StartTime, x.EndTime));
        if (ordered.SequenceEqual(intervals.OrderBy(x => x.Day).ThenBy(x => x.Start))) return true;
        db.ParkingSchedules.RemoveRange(existing);
        db.ParkingSchedules.AddRange(intervals.Select(s => new ParkingSchedule(record.SubjectId, s.Day, s.Start, s.End)));
        await db.SaveChangesAsync(ct);
        await EnqueueAsync(VerificationType.Schedule, record.SubjectId, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }, ct);

    public Task<bool> RequestCorScheduleReviewAsync(Guid owner, Guid recordId, int version, string reason, CancellationToken ct) => Task.FromResult(false);
    public Task<bool> CorrectCorScheduleAsync(Guid recordId, int version, Guid actor, List<CorWeekDay> week, string reason, CancellationToken ct) => Task.FromResult(false);

    public Task<VerificationPolicy> GetPolicyAsync(CancellationToken ct) => db.Set<VerificationPolicy>().AsNoTracking().SingleAsync(ct);

    private Task<T> Transaction<T>(Func<Task<T>> action, CancellationToken ct) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        if (db.Database.CurrentTransaction != null) return await action();
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await action();
        await transaction.CommitAsync(ct);
        return result;
    });

    private async Task Lock(VerificationType type, Guid id, CancellationToken ct)
    {
        if (type == VerificationType.Schedule)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"Cor" + id}, 0))", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({type.ToString() + id}, 0))", ct);
    }
    private Task LockPolicy(CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"VerificationPolicies\" WHERE \"Id\" = 1 FOR UPDATE", ct);

    public Task<bool> UpdatePolicyAsync(VerificationPolicy request, Guid actor, CancellationToken ct) => Transaction(async () =>
    {
        await LockPolicy(ct);
        var policy = await GetPolicyAsync(ct);
        if (policy.Version != request.Version) return false;
        var before = JsonSerializer.Serialize(policy);
        request.Id = 1; request.VehicleAutomaticApproval = false; request.Version++; request.UpdatedAt = DateTime.UtcNow; request.UpdatedBy = actor;
        db.Set<VerificationPolicy>().Update(request);
        db.Entry(request).Property(x => x.Version).OriginalValue = policy.Version;
        Audit(null, actor, "PolicyChanged", JsonSerializer.Serialize(new { Before = before, After = request }));
        await db.SaveChangesAsync(ct);
        db.Entry(request).State = EntityState.Detached;
        return true;
    }, ct);

    public async Task<VerificationSubject?> GetSubjectAsync(VerificationType type, Guid id, CancellationToken ct)
    {
        CorSubmission? cor = null; Vehicle? vehicle = null;
        var ownerId = id;
        if (type is VerificationType.Cor or VerificationType.Schedule)
        {
            cor = await db.CorSubmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (cor == null) return null;
            ownerId = cor.UserAccountId;
        }
        if (type == VerificationType.Vehicle)
        {
            vehicle = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (vehicle == null) return null;
            ownerId = vehicle.OwnerId;
        }
        var user = await db.UserAccounts.AsNoTracking().Include(x => x.AuthIdentities)
            .Include(x => x.UserProfile).ThenInclude(x => x!.Student)
            .Include(x => x.UserProfile).ThenInclude(x => x!.Personnel).FirstOrDefaultAsync(x => x.Id == ownerId, ct);
        if (user == null) return null;
        var profile = user.UserProfile;
        var identifier = profile?.Student?.StudentNumber ?? profile?.Personnel?.IdCardNumber;
        var duplicate = false;
        if (type == VerificationType.Account && identifier != null)
        {
            var normalizedIdentifier = profile?.Student != null ? Student.NormalizeNumber(identifier) : Personnel.NormalizeId(identifier);
            duplicate = profile?.Student != null
                ? await db.Students.AnyAsync(x => x.UserProfileId != profile.Id && x.StudentNumber.Trim().Replace("-", "").Replace(" ", "").ToUpper() == normalizedIdentifier, ct)
                : await db.Personnel.AnyAsync(x => x.UserProfileId != profile!.Id && x.IdCardNumber.Trim().ToUpper() == normalizedIdentifier, ct);
        }
        if (vehicle != null)
        {
            var plate = NormalizePlate(vehicle.PlateNumber);
            duplicate = await db.Vehicles.AnyAsync(x => x.Id != id && x.PlateNumber.Replace("-", "").Replace(" ", "").ToUpper() == plate, ct);
        }
        var schedules = type == VerificationType.Schedule
            ? await db.ParkingSchedules.AsNoTracking().Where(x => x.SubmissionId == id).OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime)
                .Select(x => new VerificationSchedule(x.DayOfWeek, x.StartTime, x.EndTime)).ToListAsync(ct) : [];
        var subject = new VerificationSubject(id, ownerId, type, "", user.AuthIdentities.Any(x => x.IsVerified), duplicate,
            identifier, profile?.FirstName ?? "", profile?.LastName ?? "", profile?.Student?.Course, profile?.Personnel != null,
            type == VerificationType.Schedule && profile?.Personnel != null
                ? (ValidDocument(cor?.EmployeeScheduleDocumentUrl) ? cor!.EmployeeScheduleDocumentUrl : null)
                : ValidDocument(cor?.CorDocumentUrl) ? cor!.CorDocumentUrl : null, cor?.AcademicTerm, vehicle?.PlateNumber,
            vehicle?.Brand, ValidDocument(vehicle?.OrcrDocumentUrl) && ValidDocument(vehicle?.VehiclePictureUrl) && ValidDocument(profile?.DriversLicenseUrl) && ValidDocument(profile?.ProfilePictureUrl),
            type == VerificationType.Schedule && cor?.VerificationStatus == CorVerificationStatus.Verified, schedules)
        { Suspended = user.Status == AccountStatus.Suspended, Email = user.PrimaryEmail,
            DriversLicenseUrl = profile?.DriversLicenseUrl, ProfilePictureUrl = profile?.ProfilePictureUrl,
            OrcrUrl = vehicle?.OrcrDocumentUrl, VehiclePictureUrl = vehicle?.VehiclePictureUrl,
            Section = profile?.Student?.Section, YearLevel = profile?.Student?.YearLevel };
        // Approval statuses do not change the fingerprint of an account/COR/vehicle source.
        // Driver document changes invalidate vehicle review, without invalidating an already confirmed COR/schedule.
        var source = JsonSerializer.Serialize(subject);
        if (type == VerificationType.Vehicle)
            source += JsonSerializer.Serialize(new { License = profile?.DriversLicenseUrl, Photo = profile?.ProfilePictureUrl });
        var bytes = Encoding.UTF8.GetBytes(source);
        return subject with { Fingerprint = Convert.ToHexString(SHA256.HashData(bytes)) };
    }

    private static bool ValidDocument(string? value) => !string.IsNullOrWhiteSpace(value) && value != "pending";
    private static string NormalizePlate(string value) => value.Replace("-", "").Replace(" ", "").ToUpperInvariant();

    public Task<VerificationRecord?> EnqueueAsync(VerificationType type, Guid id, CancellationToken ct) => Transaction<VerificationRecord?>(async () =>
    {
        await Lock(type, id, ct);
        if (type == VerificationType.Vehicle) { await RetireVehicleJobsAsync(id, ct); return null; }
        var subject = await GetSubjectAsync(type, id, ct);
        if (subject == null) return null;
        if (type == VerificationType.Schedule && !subject.IsPersonnel) { await RetireScheduleJobsAsync(id, ct); return null; }
        if (type == VerificationType.Account && !subject.IsPersonnel &&
            !await db.UserProfiles.AnyAsync(p => p.UserAccountId == subject.UserId && (p.Admin != null || p.Guard != null), ct))
        {
            await db.VerificationRecords.Where(r => r.Type == VerificationType.Account && r.SubjectId == id && r.IsCurrent)
                .ExecuteUpdateAsync(update => update.SetProperty(r => r.IsCurrent, false).SetProperty(r => r.LeaseId, (Guid?)null)
                    .SetProperty(r => r.LeaseExpiresAt, (DateTime?)null).SetProperty(r => r.Version, r => r.Version + 1), ct);
            if (await db.UserProfiles.AnyAsync(p => p.UserAccountId == subject.UserId && p.Student != null, ct))
            {
                var account = await db.UserAccounts.FindAsync([id], ct);
                if (account != null && account.Status != AccountStatus.Suspended)
                {
                    var approvedVehicle = await db.Vehicles.AnyAsync(v => v.OwnerId == id && v.VerificationStatus == CorVerificationStatus.Verified &&
                        v.OrcrDocumentUrl != null && v.OrcrDocumentUrl != "" && v.VehiclePictureUrl != null && v.VehiclePictureUrl != "", ct);
                    var driverReady = await db.UserProfiles.AnyAsync(p => p.UserAccountId == id && p.LicenseStatus == CorVerificationStatus.Verified && p.DriversLicenseUrl != null && p.ProfilePictureUrl != null, ct);
                    account.UpdateStatus(VerificationService.StudentAccountStatus(subject, await GetPolicyAsync(ct), approvedVehicle, driverReady));
                    await db.SaveChangesAsync(ct);
                }
            }
            return null;
        }
        // Schedule onboarding creates a parent COR before any document is uploaded.
        // It must keep NotSubmitted rather than appear to have a processed upload.
        if (type == VerificationType.Cor && string.IsNullOrWhiteSpace(subject.DocumentUrl)) return null;
        var existing = await db.Set<VerificationRecord>().SingleOrDefaultAsync(x => x.Type == type && x.SubjectId == id && x.IsCurrent, ct);
        if (existing?.Fingerprint == subject.Fingerprint)
        {
            // Identical submissions retain their decision even when older handlers set
            // a legacy status to Pending. Account suspension/status remains separate.
            if (type != VerificationType.Account) { await Apply(existing, ct); await db.SaveChangesAsync(ct); }
            return existing;
        }
        if (existing != null)
        {
            if (type == VerificationType.Account && existing.Status == VerificationState.Approved)
            {
                var account = await db.UserAccounts.FindAsync([id], ct);
                if (account?.Status == AccountStatus.Active) account.UpdateStatus(AccountStatus.PendingVerification);
            }
            existing.IsCurrent = false; existing.LeaseId = null; existing.Version++;
            Audit(existing.Id, null, "Superseded", "A new source revision was submitted.");
            await db.SaveChangesAsync(ct); // Release the partial unique index before inserting the revision.
        }
        var record = new VerificationRecord { SubjectId = id, UserAccountId = subject.UserId, Type = type, Fingerprint = subject.Fingerprint };
        if (type == VerificationType.Schedule && subject.IsPersonnel)
        {
            record.Status = VerificationState.NeedsReview;
            record.Reason = "Employee schedule requires administrator review of the uploaded document.";
        }
        if (type == VerificationType.Cor && existing?.OcrOutcome == VerificationRequests.AwaitingInformationFetch)
        {
            record.OcrOutcome = VerificationRequests.AwaitingInformationFetch;
            record.Reason = "COR uploaded. Tap Fetch Information to read its contents.";
        }
        db.Add(record);
        Audit(record.Id, subject.UserId, "Submitted", "Verification queued for this source revision.");
        await Apply(record, ct);
        await db.SaveChangesAsync(ct);
        return record;
    }, ct);

    public Task<VerificationRecord?> ClaimAsync(TimeSpan lease, CancellationToken ct) => Claim(lease, null, ct);
    public Task<VerificationRecord?> ClaimCorAsync(Guid subjectId, TimeSpan lease, CancellationToken ct) => Claim(lease, subjectId, ct);
    private Task<VerificationRecord?> Claim(TimeSpan lease, Guid? corId, CancellationToken ct) => Transaction<VerificationRecord?>(async () =>
    {
        var now = DateTime.UtcNow;
        var candidate = await db.Set<VerificationRecord>().AsNoTracking().Where(x => x.OcrOutcome != VerificationRequests.AwaitingInformationFetch && x.IsCurrent && x.Type != VerificationType.Vehicle && x.Type != VerificationType.Schedule &&
            (corId == null || x.Type == VerificationType.Cor && x.SubjectId == corId &&
                x.OcrOutcome == "DigitalPdfTextExtracted" && x.ExtractedFieldsJson != null) &&
            ((x.Status == VerificationState.Pending && x.NextAttemptAt <= now) ||
             (x.Status == VerificationState.Processing && x.LeaseExpiresAt < now) ||
             (x.Status == VerificationState.NeedsReview && x.Type == VerificationType.Schedule && x.Reason.Contains(RemovedDailyLimitReason))))
            .OrderBy(x => x.NextAttemptAt).FirstOrDefaultAsync(ct);
        if (candidate == null) return null;
        await Lock(candidate.Type, candidate.SubjectId, ct);
        var record = await db.Set<VerificationRecord>().SingleAsync(x => x.Id == candidate.Id, ct);
        if (!record.IsCurrent || record.OcrOutcome == VerificationRequests.AwaitingInformationFetch || !(record.Status == VerificationState.Pending && record.NextAttemptAt <= now ||
            record.Status == VerificationState.Processing && record.LeaseExpiresAt < now ||
            record.Status == VerificationState.NeedsReview && record.Type == VerificationType.Schedule && record.Reason.Contains(RemovedDailyLimitReason))) return null;
        record.Status = VerificationState.Processing; record.LeaseId = Guid.NewGuid();
        record.Method = VerificationMethod.Automatic;
        record.LeaseExpiresAt = now + lease; record.Attempts++; record.Version++;
        record.Reason = "Verification is processing in the background.";
        Audit(record.Id, null, "Processing", $"Attempt {record.Attempts}.");
        await Apply(record, ct);
        await db.SaveChangesAsync(ct);
        return record;
    }, ct);

    public Task<bool> CompleteAsync(VerificationRecord claim, VerificationDecision decision, CancellationToken ct) => Transaction(async () =>
    {
        if (claim.Type is VerificationType.Vehicle or VerificationType.Schedule) return false;
        await LockPolicy(ct);
        var expectedLease = claim.LeaseId;
        await Lock(claim.Type, claim.SubjectId, ct);
        var record = await db.Set<VerificationRecord>().SingleAsync(x => x.Id == claim.Id, ct);
        await db.Entry(record).ReloadAsync(ct);
        if (!record.IsCurrent || record.Status != VerificationState.Processing || record.LeaseId != expectedLease || record.LeaseExpiresAt <= DateTime.UtcNow)
            return false;
        var subject = await GetSubjectAsync(record.Type, record.SubjectId, ct);
        if (subject == null || subject.Fingerprint != record.Fingerprint) return false;
        if (record.Type == VerificationType.Account && !subject.IsPersonnel) { record.IsCurrent = false; record.LeaseId = null; record.Version++; await db.SaveChangesAsync(ct); return false; }
        var policy = await GetPolicyAsync(ct);
        if (decision.PolicyVersion != policy.Version)
            decision = decision with { Status = VerificationState.NeedsReview, Reason = "Approval policy changed during processing. Admin review is required." };
        if (decision.Status == VerificationState.Approved && !(record.Type == VerificationType.Cor && !subject.IsPersonnel) && (!policy.Allows(record.Type) ||
            record.Type != VerificationType.Schedule && !decision.TrustedEvidenceSucceeded))
            decision = decision with { Status = VerificationState.PreVerified, Reason = "Automatic approval requires current policy and trusted verification. Admin review required." };
        // Recheck the current policy while holding its lock, including term/schedule limits.
        if (decision.Status == VerificationState.Approved && ParkFlow.Application.Features.Verification.VerificationService.MandatoryChecks(subject, policy).Count > 0)
            decision = decision with { Status = VerificationState.NeedsReview, Reason = "Current university rules require admin review." };
        SetDecision(record, decision, VerificationMethod.Automatic, null);
        Audit(record.Id, null, "AutomaticVerification", record.Status + ": " + record.Reason);
        await Apply(record, ct);
        await db.SaveChangesAsync(ct);
        return true;
    }, ct);

    public Task<bool> RetryAsync(VerificationRecord claim, int maxAttempts, CancellationToken ct) => Transaction(async () =>
    {
        if (claim.Type is VerificationType.Vehicle or VerificationType.Schedule) return false;
        var expectedLease = claim.LeaseId;
        await Lock(claim.Type, claim.SubjectId, ct);
        var record = await db.Set<VerificationRecord>().SingleAsync(x => x.Id == claim.Id, ct);
        await db.Entry(record).ReloadAsync(ct);
        if (!record.IsCurrent || record.Status != VerificationState.Processing || record.LeaseId != expectedLease) return false;
        record.LeaseId = null; record.LeaseExpiresAt = null; record.Version++;
        record.Status = record.Attempts >= maxAttempts ? VerificationState.NeedsReview : VerificationState.Pending;
        record.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(300, 15 * record.Attempts));
        record.Reason = record.Status == VerificationState.Pending ? "Processing failed; a retry is scheduled."
            : "Document could not be processed after retries. Resubmit a clearer document or ask an admin to review it.";
        record.OcrOutcome = "ProcessingFailed";
        Audit(record.Id, null, "ProcessingFailed", record.Reason);
        await Apply(record, ct); await db.SaveChangesAsync(ct); return true;
    }, ct);

    public Task RetireAsync(VerificationRecord claim, CancellationToken ct) => Transaction(async () =>
    {
        if (claim.Type == VerificationType.Vehicle) { await RetireVehicleJobsAsync(claim.SubjectId, ct); return true; }
        await Lock(claim.Type, claim.SubjectId, ct);
        var record = await db.VerificationRecords.SingleAsync(x => x.Id == claim.Id, ct);
        if (record.IsCurrent && record.LeaseId == claim.LeaseId)
        {
            record.IsCurrent = false; record.LeaseId = null; record.LeaseExpiresAt = null; record.Version++;
            Audit(record.Id, null, "Retired", "The source was deleted or replaced before verification finished.");
            await db.SaveChangesAsync(ct);
        }
        return true;
    }, ct);

    public Task<bool> ReviewAsync(Guid id, int version, string fingerprint, VerificationDecision decision, Guid actor, CancellationToken ct) => Transaction(async () =>
    {
        var initial = await db.Set<VerificationRecord>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (initial == null) return false;
        if (initial.Type == VerificationType.Vehicle) return false;
        if (initial.Type == VerificationType.Schedule && (await GetSubjectAsync(initial.Type, initial.SubjectId, ct))?.IsPersonnel != true) return false;
        await LockPolicy(ct); await Lock(initial.Type, initial.SubjectId, ct);
        var record = await db.Set<VerificationRecord>().SingleAsync(x => x.Id == id, ct);
        await db.Entry(record).ReloadAsync(ct);
        var subject = await GetSubjectAsync(record.Type, record.SubjectId, ct);
        if (!record.IsCurrent || record.Version != version || record.Fingerprint != fingerprint || subject?.Fingerprint != fingerprint) return false;
        if (decision.Status == VerificationState.Approved && ParkFlow.Application.Features.Verification.VerificationService.MandatoryChecks(subject, await GetPolicyAsync(ct)).Count > 0)
            return false;
        var previous = record.Status;
        SetDecision(record, decision, VerificationMethod.Manual, actor);
        Audit(record.Id, actor, "ManualReview", $"{previous} -> {record.Status}: {record.Reason}");
        await Apply(record, ct); await db.SaveChangesAsync(ct); return true;
    }, ct);

    private static void SetDecision(VerificationRecord record, VerificationDecision decision, VerificationMethod method, Guid? actor)
    {
        record.Status = decision.Status; record.Reason = decision.Reason; record.Method = method;
        record.OcrOutcome = decision.OcrOutcome ?? record.OcrOutcome;
        record.ExtractedFieldsJson = decision.ExtractedFieldsJson ?? record.ExtractedFieldsJson;
        record.VerifiedAt = DateTime.UtcNow; record.ReviewerId = actor;
        record.LeaseId = null; record.LeaseExpiresAt = null; record.UpdatedAt = DateTime.UtcNow; record.Version++;
    }

    private async Task Apply(VerificationRecord record, CancellationToken ct)
    {
        var status = record.Status switch
        {
            VerificationState.Approved => CorVerificationStatus.Verified,
            VerificationState.Rejected => CorVerificationStatus.Rejected,
            VerificationState.Processing => CorVerificationStatus.Processing,
            VerificationState.PreVerified => CorVerificationStatus.PreVerified,
            VerificationState.NeedsReview => CorVerificationStatus.NeedsReview,
            _ => CorVerificationStatus.Pending
        };
        if (record.Type is VerificationType.Cor or VerificationType.Schedule)
        {
            var cor = await db.CorSubmissions.FindAsync([record.SubjectId], ct);
            if (cor == null) return;
            await db.Entry(cor).ReloadAsync(ct);
            if (record.Type == VerificationType.Cor) cor.UpdateSubmission(null, null, status, rejectionReason: record.Reason);
            else cor.UpdateScheduleVerification(status);
        }
        else if (record.Type == VerificationType.Vehicle)
        {
            var vehicle = await db.Vehicles.FindAsync([record.SubjectId], ct);
            if (vehicle != null) { await db.Entry(vehicle).ReloadAsync(ct); vehicle.UpdateVerificationStatus(status, record.Reason); }
        }
        else if (record.Status is VerificationState.Approved or VerificationState.Rejected)
        {
            var user = await db.UserAccounts.FindAsync([record.SubjectId], ct);
            if (user != null)
            {
                await db.Entry(user).ReloadAsync(ct);
                if (user.Status != AccountStatus.Suspended)
                    user.UpdateStatus(record.Status == VerificationState.Approved ? AccountStatus.Active : AccountStatus.PendingVerification);
            }
        }
        await RefreshPersonnelStatusAsync(record.UserAccountId, ct);
    }

    private async Task RefreshPersonnelStatusAsync(Guid userId, CancellationToken ct)
    {
        if (!await db.Personnel.AnyAsync(p => p.UserProfile.UserAccountId == userId, ct)) return;
        await db.SaveChangesAsync(ct);
        var user = await db.UserAccounts.FindAsync([userId], ct);
        if (user == null || user.Status == AccountStatus.Suspended) return;
        var identity = await db.VerificationRecords.AnyAsync(r => r.UserAccountId == userId && r.Type == VerificationType.Account && r.IsCurrent && r.Status == VerificationState.Approved, ct);
        var schedule = await db.CorSubmissions.AnyAsync(c => c.UserAccountId == userId && c.VerificationStatus == CorVerificationStatus.Verified && c.ScheduleVerificationStatus == CorVerificationStatus.Verified &&
            db.VerificationRecords.Any(r => r.Type == VerificationType.Schedule && r.SubjectId == c.Id && r.IsCurrent && r.Status == VerificationState.Approved), ct);
        var vehicle = await db.Vehicles.AnyAsync(v => v.OwnerId == userId && v.VerificationStatus == CorVerificationStatus.Verified && v.OrcrDocumentUrl != null && v.VehiclePictureUrl != null, ct);
        var driver = await db.UserProfiles.AnyAsync(p => p.UserAccountId == userId && p.LicenseStatus == CorVerificationStatus.Verified && p.DriversLicenseUrl != null && p.ProfilePictureUrl != null, ct);
        user.UpdateStatus(identity && schedule && vehicle && driver ? AccountStatus.Active : AccountStatus.PendingVerification);
    }

    private void Audit(Guid? record, Guid? actor, string action, string details)
    {
        if (record.HasValue)
        {
            var source = db.VerificationRecords.Local.FirstOrDefault(r => r.Id == record.Value) ?? db.VerificationRecords.AsNoTracking().FirstOrDefault(r => r.Id == record.Value);
            if (source != null && source.Type is VerificationType.Schedule or VerificationType.Cor or VerificationType.Account &&
                !db.UserProfiles.Any(p => p.UserAccountId == source.UserAccountId && p.Personnel != null)) return;
        }
        db.Add(new VerificationAudit { VerificationRecordId = record, ActorId = actor, Action = action, Details = details });
    }
    public Task<List<VerificationRecord>> ListAsync(Guid? userId, bool history, CancellationToken ct) =>
        db.Set<VerificationRecord>().AsNoTracking().Where(x => x.Type != VerificationType.Vehicle &&
            db.UserProfiles.Any(p => p.UserAccountId == x.UserAccountId && p.Personnel != null) && (userId == null || x.UserAccountId == userId) && (history || x.IsCurrent))
            .OrderByDescending(x => x.CreatedAt).Take(1000).ToListAsync(ct);
    public Task<VerificationRecord?> FindAsync(Guid id, CancellationToken ct) =>
        db.VerificationRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<List<VerificationAudit>> AuditAsync(Guid? recordId, CancellationToken ct) => db.Set<VerificationAudit>().AsNoTracking()
        .Where(x => x.VerificationRecordId == recordId).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
}
