using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Files.Commands.UploadCorDocument;

public class UploadCorDocumentHandler : IRequestHandler<UploadCorDocumentCommand, Result<UploadFileResponse>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly ICloudinaryService _cloudinaryService;

    public UploadCorDocumentHandler(ICorSubmissionRepository corSubmissionRepository, ICloudinaryService cloudinaryService)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<Result<UploadFileResponse>> Handle(UploadCorDocumentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var corSubmission = await _corSubmissionRepository.GetCorSubmissionAsync(request.CorSubmissionId);
            if (corSubmission == null)
            {
                return Result<UploadFileResponse>.Failure("COR submission record not found.", ErrorCode.NotFound);
            }

            // Auto-delete previous document from Cloudinary if it exists in the database
            if (!string.IsNullOrWhiteSpace(corSubmission.CorDocumentUrl))
            {
                var previousPublicId = CloudinaryUrlParser.ExtractPublicId(corSubmission.CorDocumentUrl);
                if (!string.IsNullOrWhiteSpace(previousPublicId))
                {
                    try
                    {
                        var isPreviousPdf = corSubmission.CorDocumentUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
                        await _cloudinaryService.DeleteFileAsync(previousPublicId, isImage: !isPreviousPdf);
                    }
                    catch
                    {
                        // Ignore deletion failure to avoid blocking the new upload
                    }
                }
            }

            // Upload the new document (PDF or Image)
            var isPdf = request.File.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            var (secureUrl, publicId) = isPdf
                ? await _cloudinaryService.UploadPdfAsync(request.File, "parkflow/cor")
                : await _cloudinaryService.UploadImageAsync(request.File, "parkflow/cor");

            // Update database record
            corSubmission.UpdateSubmission(null, secureUrl, null);
            await _corSubmissionRepository.UpdateCorSubmissionAsync(corSubmission);

            var response = new UploadFileResponse(secureUrl, publicId);
            return Result<UploadFileResponse>.Success(response, "COR document updated successfully.");
        }
        catch (Exception ex)
        {
            return Result<UploadFileResponse>.Failure($"COR document upload failed: {ex.Message}", ErrorCode.ServerError);
        }
    }
}
