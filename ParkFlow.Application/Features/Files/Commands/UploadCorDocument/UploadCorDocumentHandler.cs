using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Files.Commands.UploadCorDocument;

public class UploadCorDocumentHandler : IRequestHandler<UploadCorDocumentCommand, Result<UploadFileResponse>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IUserContext _userContext;

    public UploadCorDocumentHandler(
        ICorSubmissionRepository corSubmissionRepository,
        ICloudinaryService cloudinaryService,
        IUserContext userContext)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _cloudinaryService = cloudinaryService;
        _userContext = userContext;
    }

    public async Task<Result<UploadFileResponse>> Handle(UploadCorDocumentCommand request, CancellationToken cancellationToken)
    {
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
                corSubmission.UpdateSubmission(null, secureUrl, null);
                await _corSubmissionRepository.UpdateCorSubmissionAsync(corSubmission);
            }

            var response = new UploadFileResponse(secureUrl, publicId);
            return Result<UploadFileResponse>.Success(response, "COR document uploaded successfully.");
        }
        catch (Exception ex)
        {
            return Result<UploadFileResponse>.Failure($"COR document upload failed: {ex.Message}", ErrorCode.ServerError);
        }
    }
}
