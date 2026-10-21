using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ParkFlow.Application.Features.Verification;

namespace ParkFlow.Application.Features.Files.Commands.UploadCorDocument;

public class UploadCorDocumentHandler : IRequestHandler<UploadCorDocumentCommand, Result<UploadFileResponse>>
{
    private readonly ICorSubmissionRepository cors;
    private readonly ICloudinaryService cloud;
    private readonly IUserContext user;
    private readonly IValidator<UploadCorDocumentCommand>? validator;
    private readonly ICorDocumentReader? reader;
    private readonly IVerificationStore? verification;
    private readonly IParkingLogRepository? parking;
    private readonly VerificationService? processor;
    private readonly ILogger<UploadCorDocumentHandler>? logger;
    public UploadCorDocumentHandler(ICorSubmissionRepository corSubmissionRepository, ICloudinaryService cloudinaryService,
        IUserContext userContext, IValidator<UploadCorDocumentCommand>? validator = null,
        IVehicleRepository? vehicleRepository = null, IUserAccountRepository? userAccountRepository = null,
        ISignalRNotificationSender? signalRNotificationSender = null, ICorDocumentReader? documentReader = null,
        IVerificationStore? verificationStore = null, IParkingLogRepository? parkingLogRepository = null,
        VerificationService? verificationService = null, ILogger<UploadCorDocumentHandler>? logger = null)
    { cors = corSubmissionRepository; cloud = cloudinaryService; user = userContext; this.validator = validator;
        reader = documentReader; verification = verificationStore; parking = parkingLogRepository;
        processor = verificationService; this.logger = logger; }

    public async Task<Result<UploadFileResponse>> Handle(UploadCorDocumentCommand request, CancellationToken ct)
    {
        var owner = user.GetUserId();
        if (owner == Guid.Empty) return Result<UploadFileResponse>.Failure("Sign in before uploading documents.", ErrorCode.Unauthorized);
        if (validator != null)
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid) return Result<UploadFileResponse>.Failure(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)), ErrorCode.BadRequest);
        }
        var submission = request.CorSubmissionId.HasValue ? await cors.GetCorSubmissionAsync(request.CorSubmissionId.Value) : await cors.GetLatestByUserIdAsync(owner);
        if (request.CorSubmissionId.HasValue && submission == null) return Result<UploadFileResponse>.Failure("Submission not found.", ErrorCode.NotFound);
        if (submission != null && submission.UserAccountId != owner) return Result<UploadFileResponse>.Failure("This submission belongs to another account.", ErrorCode.Forbidden);
        if (parking != null && await parking.HasActiveParkingLogByUserIdAsync(owner))
            return Result<UploadFileResponse>.Failure("Exit the parking facility before replacing your document.", ErrorCode.BadRequest);
        var stage = "ReadingDocument";
        var reference = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        try
        {
            var watch = Stopwatch.StartNew();
            var previousUrl = submission?.CorDocumentUrl;
            var personnel = verification != null &&
                (await verification.GetSubjectAsync(ParkFlow.Domain.Entities.VerificationType.Account, owner, ct))?.IsPersonnel == true;
            if (reader != null)
            {
                await using var stream = request.File.OpenReadStream();
                // Validate file contents/safety at upload; extraction starts only after Fetch Information.
                await reader.ValidateAsync(stream, request.File.FileName, ct);
            }
            logger?.LogInformation("COR upload validation in {DurationMs} ms", watch.Elapsed.TotalMilliseconds);
            watch.Restart();
            var pdf = Path.GetExtension(request.File.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
            stage = "UploadingToStorage";
            var (url, publicId) = pdf ? await cloud.UploadPdfAsync(request.File, "parkflow/cor") : await cloud.UploadImageAsync(request.File, "parkflow/cor");
            logger?.LogInformation("COR storage upload in {DurationMs} ms", watch.Elapsed.TotalMilliseconds); watch.Restart();
            ct.ThrowIfCancellationRequested();
            var create = submission == null;
            stage = "LoadingVerificationSettings";
            var policy = verification == null ? null : await verification.GetPolicyAsync(ct);
            if (create)
            {
                submission = new CorSubmission(owner, policy?.AcademicYear ?? "", url, verificationStatus: CorVerificationStatus.Pending);
            }
            else
            {
                submission!.UpdateSubmission(submission.AcademicTerm == "Employee ID" ? null : policy?.AcademicYear, url, CorVerificationStatus.Pending);
            }
            stage = "SavingDocumentAndVerification";
            await cors.SaveUploadedAsync(submission!, create, null, personnel ? null : VerificationRequests.AwaitingInformationFetch, ct);
            logger?.LogInformation("COR source/job database save in {DurationMs} ms", watch.Elapsed.TotalMilliseconds);
            stage = "PreparingPreview";
            CorSchedulePreview? preview = null;
            if (!personnel && processor != null && verification != null)
            {
                try { preview = await new VerificationRequests(verification, processor).Handle(new GetCorSchedulePreview(owner), ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch { logger?.LogWarning("COR uploaded; preview can be retrieved by refreshing its status."); }
            }
            // Preserve the existing replacement policy, but only remove the previous
            // blob after the replacement and its durable job have committed.
            if (!string.IsNullOrWhiteSpace(previousUrl) && previousUrl != "pending" && previousUrl != url)
            {
                var previousId = CloudinaryUrlParser.ExtractPublicId(previousUrl);
                if (!string.IsNullOrWhiteSpace(previousId))
                {
                    try { await cloud.DeleteFileAsync(previousId, !previousUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)); }
                    catch { /* A storage cleanup failure must not undo a successful submission. */ }
                }
            }
            return Result<UploadFileResponse>.Success(new UploadFileResponse(url, publicId)
                { SubmissionId = submission!.Id, VerificationStatus = preview?.Status ?? "Pending", CorPreview = preview },
                personnel ? "Document uploaded. Verification will run in the background." : "COR uploaded. Tap Fetch Information to read your student details and schedule.");
        }
        catch (DocumentValidationException ex) { return Result<UploadFileResponse>.Failure(ex.Message, ErrorCode.BadRequest); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Do not log exception messages: storage/native/database exceptions may include document content or credentials.
            var cause = ex.GetBaseException();
            logger?.LogError("COR upload failed. Reference {Reference}; stage {Stage}; exception {ExceptionType}; cause {CauseType}; code {ErrorCode}; stack {StackTrace}",
                reference, stage, ex.GetType().FullName, cause.GetType().FullName, cause.HResult, cause.StackTrace);
            var action = stage switch
            {
                "ReadingDocument" or "ExtractingInformation" => "processing the document",
                "UploadingToStorage" => "saving the file to document storage",
                "LoadingVerificationSettings" => "loading the server verification settings",
                "SavingDocumentAndVerification" => "saving the document registration",
                _ => "preparing the schedule preview"
            };
            return Result<UploadFileResponse>.Failure($"COR upload failed while {action}. Reference: {reference}. Please contact the administrator with this reference.", ErrorCode.ServerError);
        }
    }
}
