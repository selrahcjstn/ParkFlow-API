using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Files.Commands.UploadCorDocument;

public class UploadCorDocumentHandler : IRequestHandler<UploadCorDocumentCommand, Result<UploadFileResponse>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IUserContext _userContext;
    private readonly IValidator<UploadCorDocumentCommand>? _validator;
    private readonly IVehicleRepository? _vehicleRepository;
    private readonly IUserAccountRepository? _userAccountRepository;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    public UploadCorDocumentHandler(
        ICorSubmissionRepository corSubmissionRepository,
        ICloudinaryService cloudinaryService,
        IUserContext userContext,
        IValidator<UploadCorDocumentCommand>? validator = null,
        IVehicleRepository? vehicleRepository = null,
        IUserAccountRepository? userAccountRepository = null,
        ISignalRNotificationSender? signalRNotificationSender = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _cloudinaryService = cloudinaryService;
        _userContext = userContext;
        _validator = validator;
        _vehicleRepository = vehicleRepository;
        _userAccountRepository = userAccountRepository;
        _signalRNotificationSender = signalRNotificationSender;
    }

    public async Task<Result<UploadFileResponse>> Handle(UploadCorDocumentCommand request, CancellationToken cancellationToken)
    {
        if (_validator != null)
        {
            var validationResult = await _validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                return Result<UploadFileResponse>.Failure(errors, ErrorCode.BadRequest);
            }
        }

        try
        {
            CorSubmission? corSubmission = null;
            if (request.CorSubmissionId.HasValue && request.CorSubmissionId.Value != Guid.Empty)
            {
                corSubmission = await _corSubmissionRepository.GetCorSubmissionAsync(request.CorSubmissionId.Value);
            }

            var currentUserId = _userContext.GetUserId();
            if (corSubmission == null && currentUserId != Guid.Empty)
            {
                corSubmission = await _corSubmissionRepository.GetLatestByUserIdAsync(currentUserId);
            }

            if (corSubmission == null && currentUserId != Guid.Empty)
            {
                var newSubmission = new CorSubmission(currentUserId, "2025-2026", "pending");
                await _corSubmissionRepository.AddCorSubmissionAsync(newSubmission);
                corSubmission = newSubmission;
            }

            // Delete previous document if submission exists
            if (corSubmission != null && !string.IsNullOrWhiteSpace(corSubmission.CorDocumentUrl))
            {
                var oldPublicId = CloudinaryUrlParser.ExtractPublicId(corSubmission.CorDocumentUrl);
                if (!string.IsNullOrWhiteSpace(oldPublicId))
                {
                    try
                    {
                        var isPreviousPdf = corSubmission.CorDocumentUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
                        await _cloudinaryService.DeleteFileAsync(oldPublicId, isImage: !isPreviousPdf);
                    }
                    catch
                    {
                    }
                }
            }

            // Upload the new document (PDF or Image)
            var isPdf = request.File.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            var (secureUrl, publicId) = isPdf
                ? await _cloudinaryService.UploadPdfAsync(request.File, "parkflow/cor")
                : await _cloudinaryService.UploadImageAsync(request.File, "parkflow/cor");

            // Update database record if submission exists
            if (corSubmission != null)
            {
                corSubmission.UpdateSubmission(null, secureUrl, ParkFlow.Domain.Enums.CorVerificationStatus.Pending);
                await _corSubmissionRepository.UpdateCorSubmissionAsync(corSubmission);

                // Reset user account status to PendingVerification
                if (_userAccountRepository != null)
                {
                    var user = await _userAccountRepository.GetByIdAsync(corSubmission.UserAccountId);
                    if (user != null)
                    {
                        user.UpdateStatus(ParkFlow.Domain.Enums.AccountStatus.PendingVerification);
                        await _userAccountRepository.UpdateAsync(user);
                    }
                }

                if (_signalRNotificationSender != null)
                {
                    try
                    {
                        await _signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", new
                        {
                            type = "schedule",
                            submissionId = corSubmission.Id,
                            userId = corSubmission.UserAccountId,
                            status = "Pending"
                        });

                        await _signalRNotificationSender.SendToUserAsync(corSubmission.UserAccountId.ToString(), "VerificationStatusChanged", new
                        {
                            userId = corSubmission.UserAccountId,
                            type = "cor_updated",
                            status = "Pending"
                        });
                    }
                    catch
                    {
                    }
                }
            }

            var response = new UploadFileResponse(secureUrl, publicId);
            return Result<UploadFileResponse>.Success(response, "COR document uploaded successfully.");
        }
        catch (Exception ex)
        {
            return Result<UploadFileResponse>.Failure($"COR document upload failed: {ex.Message}", ErrorCode.BadRequest);
        }
    }
}
