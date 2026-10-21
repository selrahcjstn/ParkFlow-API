using System.Security.Cryptography;
using System.Threading.Channels;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Auth.Registration;
using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Infrastructure.Verification;

// Temporary, bounded memory only. These sessions are not login tokens or accounts.
public sealed class RegistrationDraftService(IServiceScopeFactory scopes, ILogger<RegistrationDraftService> logger)
    : BackgroundService, IRegistrationDraftService
{
    private readonly MemoryCache sessions = new(new MemoryCacheOptions { SizeLimit = 64 * 1024 * 1024 });
    private readonly Channel<(Draft Draft, Guid DocumentId)> queue = Channel.CreateBounded<(Draft, Guid)>(32);
    private sealed record StoredUpload(string Url, string PublicId, bool IsImage);
    private sealed class Draft(string email)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Email { get; } = email;
        public DateTime ExpiresAt { get; } = DateTime.UtcNow.AddHours(1);
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public byte[]? Bytes;
        public string FileName = "";
        public CorSchedulePreview? Preview;
        public CorFields? Fields;
        public Result<string>? Completed;
        public Dictionary<string, StoredUpload> Uploads { get; } = [];
    }
    private static string Key(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    private Draft Get(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 64 || !sessions.TryGetValue<Draft>(Key(token), out var draft) || draft == null || draft.ExpiresAt <= DateTime.UtcNow)
            throw new DocumentValidationException("Your temporary registration expired. Return to email and password and verify your email again.");
        return draft;
    }
    public Task<RegistrationSession> StartAsync(string email, string emailVerificationToken, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(emailVerificationToken) || !scope.ServiceProvider.GetRequiredService<IEmailVerificationReceipt>().Validate(email, emailVerificationToken))
            throw new DocumentValidationException("Email verification expired or is invalid. Request a new verification code.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var draft = new Draft(email.Trim().ToLowerInvariant());
        sessions.Set(Key(token), draft, new MemoryCacheEntryOptions { AbsoluteExpiration = draft.ExpiresAt, Size = 1024 });
        return Task.FromResult(new RegistrationSession(token, draft.ExpiresAt));
    }
    public async Task<CorSchedulePreview> UploadCorAsync(string token, IFormFile file, CancellationToken ct)
    {
        var draft = Get(token);
        await draft.Gate.WaitAsync(ct);
        try
        {
            if (draft.Completed != null) throw new DocumentValidationException("This registration has already been submitted.");
            using var scope = scopes.CreateScope();
            await ValidateFileAsync(file, "cor", scope.ServiceProvider, ct);
            using var buffer = new MemoryStream(); await file.CopyToAsync(buffer, ct);
            draft.Bytes = buffer.ToArray(); draft.FileName = Path.GetFileName(file.FileName); draft.Fields = null;
            var id = Guid.NewGuid();
            draft.Preview = new(id, 1, "Pending", "registration-cor:" + id, [], [], false) { FetchRequested = false };
            sessions.Set(Key(token), draft, new MemoryCacheEntryOptions { AbsoluteExpiration = draft.ExpiresAt, Size = draft.Bytes.Length + 1024 });
            if (!sessions.TryGetValue(Key(token), out _)) throw new DocumentValidationException("COR processing is busy. Please try uploading again shortly.");
            return CorAcademicTerm.Preview(draft.Preview, null);
        }
        finally { draft.Gate.Release(); }
    }
    public async Task<CorSchedulePreview?> PreviewAsync(string token, CancellationToken ct)
    {
        var draft = Get(token); await draft.Gate.WaitAsync(ct);
        try { return draft.Preview == null ? null : CorAcademicTerm.Preview(draft.Preview, draft.Fields); } finally { draft.Gate.Release(); }
    }
    public async Task<CorSchedulePreview> FetchAsync(string token, Guid recordId, int version, CancellationToken ct)
    {
        var draft = Get(token); await draft.Gate.WaitAsync(ct);
        try
        {
            var preview = draft.Preview;
            if (preview == null || preview.RecordId != recordId || preview.Version != version || draft.Bytes == null)
                throw new DocumentValidationException("The COR changed. Refresh its status and try again.");
            if (preview.FetchRequested && preview.Status != "NeedsReview") return CorAcademicTerm.Preview(preview, draft.Fields);
            if (!queue.Writer.TryWrite((draft, recordId))) throw new DocumentValidationException("COR processing is busy. Tap Fetch Information again shortly.");
            draft.Preview = preview with { Status = "Processing", FetchRequested = true, Issues = [] };
            return CorAcademicTerm.Preview(draft.Preview, null);
        }
        finally { draft.Gate.Release(); }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            byte[]? bytes; string name;
            await job.Draft.Gate.WaitAsync(stoppingToken);
            try
            {
                if (job.Draft.Preview?.RecordId != job.DocumentId || job.Draft.ExpiresAt <= DateTime.UtcNow) continue;
                bytes = job.Draft.Bytes; name = job.Draft.FileName;
            }
            finally { job.Draft.Gate.Release(); }
            if (bytes == null) continue;
            CorFields? fields = null; string? error = null;
            try
            {
                using var scope = scopes.CreateScope();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(scope.ServiceProvider.GetRequiredService<IOptions<OcrOptions>>().Value.JobTimeoutSeconds));
                using var stream = new MemoryStream(bytes, writable: false);
                var read = await scope.ServiceProvider.GetRequiredService<ICorDocumentReader>().ReadAsync(stream, name, timeout.Token);
                fields = CorTextVerification.Extract(read.Text);
                if (string.IsNullOrWhiteSpace(read.Text)) error = "Document is unreadable. Upload a clearer COR.";
            }
            catch (Exception ex)
            {
                // Never log document text, credentials, tokens or request bodies.
                logger.LogWarning("Temporary COR extraction failed ({ExceptionType}).", ex.GetType().Name);
                error = "The COR could not be read. Upload a clearer COR or tap Fetch Information to retry.";
            }
            await job.Draft.Gate.WaitAsync(stoppingToken);
            try
            {
                if (job.Draft.Preview?.RecordId != job.DocumentId) continue;
                var week = fields == null ? new List<CorWeekDay>() : CorStudentMapping.AuthoritativeWeek(fields);
                var issues = fields?.ScheduleIssues.ToList() ?? [];
                if (error != null) issues.Add(error);
                var valid = fields != null && error == null && issues.Count == 0 && CorStudentMapping.ValidateWeek(week, out _).Count == 0;
                job.Draft.Fields = error == null ? fields : null;
                job.Draft.Preview = job.Draft.Preview with { Status = valid ? "PreVerified" : "NeedsReview", CanConfirm = fields != null && error == null,
                    CanConfirmSchedule = valid, Week = week, StudentInformation = fields == null ? null : CorStudentMapping.Information(fields), Issues = issues };
            }
            finally { job.Draft.Gate.Release(); }
        }
    }
    public async Task<Result<string>> CompleteAsync(string token, RegistrationDetails details, IFormFile? cor, IFormFile orcr, IFormFile motor, CancellationToken ct)
    {
        var draft = Get(token); await draft.Gate.WaitAsync(ct);
        try
        {
            if (draft.Completed != null) return draft.Completed;
            using var scope = scopes.CreateScope();
            var services = scope.ServiceProvider;
            using var corStream = details.Role == Roles.Student && draft.Bytes != null ? new MemoryStream(draft.Bytes, writable: false) : null;
            var corFile = corStream == null ? cor : new FormFile(corStream, 0, corStream.Length, "cor", draft.FileName);
            if (corFile == null || orcr == null || motor == null) throw new DocumentValidationException("All registration documents are required.");
            var command = new CompleteRegistration(draft.Id, details, draft.Email, "staged", "staged", "staged", draft.Fields);
            var errors = await CompleteRegistrationHandler.ValidateAsync(command, ct);
            if (errors.Count > 0) return Result<string>.Failure(string.Join(" ", errors.Distinct()), ErrorCode.BadRequest);
            await ValidateFileAsync(corFile, "cor", services, ct);
            await ValidateFileAsync(orcr, "orcr", services, ct);
            await ValidateFileAsync(motor, "motor", services, ct);
            var cloud = services.GetRequiredService<ICloudinaryService>();
            async Task<string> Upload(IFormFile file, string folder)
            {
                using var content = file.OpenReadStream();
                var key = folder + ":" + Convert.ToHexString(await SHA256.HashDataAsync(content, ct));
                if (draft.Uploads.TryGetValue(key, out var existing)) return existing.Url;
                var image = !Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
                var uploaded = image ? await cloud.UploadImageAsync(file, folder) : await cloud.UploadPdfAsync(file, folder);
                draft.Uploads[key] = new(uploaded.SecureUrl, uploaded.PublicId, image);
                return uploaded.SecureUrl;
            }
            var corUrl = await Upload(corFile, "parkflow/cor");
            var orcrUrl = await Upload(orcr, "parkflow/orcr");
            var motorUrl = await Upload(motor, "parkflow/motor-pictures");
            var result = await services.GetRequiredService<IMediator>().Send(command with { CorUrl = corUrl, OrcrUrl = orcrUrl, MotorUrl = motorUrl }, ct);
            if (result.IsSuccess)
            {
                draft.Bytes = null; draft.Fields = null; draft.Preview = null; draft.FileName = "";
                draft.Completed = result;
                draft.Uploads.Clear(); // These files are now owned by the committed records.
                sessions.Set(Key(token), draft, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15), Size = 1024 });
            }
            else
            {
                // A definite validation/conflict response rolled back the database.
                // On transport/commit exceptions retain upload receipts for a safe retry.
                foreach (var upload in draft.Uploads.Values) await DeleteUploadAsync(upload, cloud);
                draft.Uploads.Clear();
            }
            return result;
        }
        finally { details.Password = ""; draft.Gate.Release(); }
    }
    private static async Task ValidateFileAsync(IFormFile file, string kind, IServiceProvider services, CancellationToken ct)
    {
        var result = kind switch
        {
            "cor" => await new ParkFlow.Application.Features.Files.Commands.UploadCorDocument.UploadCorDocumentCommandValidator()
                .ValidateAsync(new ParkFlow.Application.Features.Files.Commands.UploadCorDocument.UploadCorDocumentCommand(file), ct),
            "orcr" => await new ParkFlow.Application.Features.Files.Commands.UploadOrcrDocument.UploadOrcrDocumentCommandValidator()
                .ValidateAsync(new ParkFlow.Application.Features.Files.Commands.UploadOrcrDocument.UploadOrcrDocumentCommand(file), ct),
            _ => await new ParkFlow.Application.Features.Files.Commands.UploadMotorPicture.UploadMotorPictureCommandValidator()
                .ValidateAsync(new ParkFlow.Application.Features.Files.Commands.UploadMotorPicture.UploadMotorPictureCommand(file), ct)
        };
        if (!result.IsValid) throw new DocumentValidationException(string.Join(" ", result.Errors.Select(e => e.ErrorMessage)));
        if (kind == "cor")
        {
            using var stream = file.OpenReadStream();
            await services.GetRequiredService<ICorDocumentReader>().ValidateAsync(stream, file.FileName, ct);
        }
    }
    public async Task CancelAsync(string token, CancellationToken ct)
    {
        var draft = Get(token); await draft.Gate.WaitAsync(ct);
        try { draft.Bytes = null; draft.Fields = null; draft.Preview = null; sessions.Remove(Key(token)); }
        finally { draft.Gate.Release(); }
    }
    private static async Task DeleteUploadAsync(StoredUpload upload, ICloudinaryService cloud)
    {
        if (!upload.Url.StartsWith("uploads/parkflow/", StringComparison.Ordinal))
        { await cloud.DeleteFileAsync(upload.PublicId, upload.IsImage); return; }
        // Only delete the exact server-generated file inside the local upload root.
        var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "parkflow")) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", upload.Url.Replace('/', Path.DirectorySeparatorChar)));
        if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            try { File.Delete(path); }
            catch (IOException) { /* Best-effort storage compensation. */ }
            catch (UnauthorizedAccessException) { /* The transaction still rolled back. */ }
    }
    public override void Dispose() { sessions.Dispose(); base.Dispose(); }
}
