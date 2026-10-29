using ParkFlow.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ParkFlow.Application.Features.Verification;

namespace ParkFlow.Persistence.Repositories;

public class CorSubmissionRepository : ICorSubmissionRepository
{
    private readonly AppDbContext _appDbContext;

    public CorSubmissionRepository(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }
    public Task SaveEmployeeScheduleAsync(CorSubmission submission) => VerificationMutation.RunAsync(
        _appDbContext,ParkFlow.Domain.Entities.VerificationType.Schedule,submission.Id,async () =>
        {
            // Changing a schedule must not replace or re-approve the employee identity document.
            var entry=_appDbContext.Entry(submission);entry.State=EntityState.Unchanged;
            entry.Property(c=>c.EmployeeScheduleDocumentUrl).IsModified=true;
            entry.Property(c=>c.ScheduleVerificationStatus).IsModified=true;
            entry.Property(c=>c.UpdatedAt).IsModified=true;
            await _appDbContext.SaveChangesAsync();
        });
    
    public async Task AddCorSubmissionAsync(CorSubmission corSubmission)
    {
        await VerificationMutation.RunAsync(_appDbContext, ParkFlow.Domain.Entities.VerificationType.Cor, corSubmission.Id, async () =>
        {
            await _appDbContext.CorSubmissions.AddAsync(corSubmission);
            await _appDbContext.SaveChangesAsync();
        });
    }

    public Task SaveUploadedAsync(CorSubmission submission, bool create, CorFields? fields, string? outcome, CancellationToken ct) =>
        VerificationMutation.RunAsync(_appDbContext, ParkFlow.Domain.Entities.VerificationType.Cor, submission.Id, async () =>
        {
            if (create) await _appDbContext.CorSubmissions.AddAsync(submission, ct);
            else
            {
                var entry = _appDbContext.Entry(submission);
                if (entry.State == EntityState.Detached) _appDbContext.CorSubmissions.Update(submission);
                entry.Property(x => x.ScheduleVerificationStatus).IsModified = false;
            }
            await _appDbContext.SaveChangesAsync(ct);
        }, async record =>
        {
            if (record == null || record.Status != ParkFlow.Domain.Entities.VerificationState.Pending) return;
            // Check the persisted role; a client token cannot opt Personnel into
            // Student extraction or change its employee-ID review behavior.
            var personnel = await _appDbContext.UserProfiles.AnyAsync(p => p.UserAccountId == submission.UserAccountId && p.Personnel != null, ct);
            if (personnel) return;
            if (outcome == VerificationRequests.AwaitingInformationFetch)
            {
                record.OcrOutcome = outcome;
                record.Reason = "COR uploaded. Tap Fetch Information to read its contents.";
                await _appDbContext.SaveChangesAsync(ct);
                return;
            }
            if (fields == null) return;
            record.ExtractedFieldsJson = JsonSerializer.Serialize(fields with { SourceDocumentUrl = submission.CorDocumentUrl });
            record.OcrOutcome = outcome;
            await _appDbContext.SaveChangesAsync(ct);
        }, ct);

    public async Task DeleteCorSubmissionAsync(CorSubmission corSubmission)
    {
        _appDbContext.CorSubmissions.Remove(corSubmission);
        await _appDbContext.SaveChangesAsync();
    }

    public Task<CorSubmission?> GetCorSubmissionAsync(Guid id)
    {
        return _appDbContext.CorSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public Task<CorSubmission?> GetByUserIdAndTermAsync(Guid userAccountId, string academicTerm)
    {
        return _appDbContext.CorSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserAccountId == userAccountId && x.AcademicTerm == academicTerm);
    }

    public Task<CorSubmission?> GetLatestByUserIdAsync(Guid userAccountId)
    {
        return _appDbContext.CorSubmissions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.UserAccountId == userAccountId);
    }

    public async Task<IEnumerable<CorSubmission>> GetByUserIdsAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        return await _appDbContext.CorSubmissions.AsNoTracking()
            .Where(cor => ids.Contains(cor.UserAccountId))
            .OrderByDescending(cor => cor.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<CorSubmission>> ListCorSubmissionsAsync()
    {
        return await _appDbContext.CorSubmissions
            .Include(x => x.UserAccount)
                .ThenInclude(u => u.UserProfile)
                    .ThenInclude(p => p!.Personnel)
            .Include(x => x.UserAccount)
                .ThenInclude(u => u.AuthIdentities)
            .OrderByDescending(x => x.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task UpdateCorSubmissionAsync(CorSubmission corSubmission)
    {
        await VerificationMutation.RunAsync(_appDbContext, ParkFlow.Domain.Entities.VerificationType.Cor, corSubmission.Id, async () =>
        {
            var entry = _appDbContext.Entry(corSubmission);
            if (entry.State == EntityState.Detached) _appDbContext.CorSubmissions.Update(corSubmission);
            // COR edits cannot overwrite an independently reviewed schedule.
            _appDbContext.Entry(corSubmission).Property(x => x.ScheduleVerificationStatus).IsModified = false;
            await _appDbContext.SaveChangesAsync();
        });
    }
}
