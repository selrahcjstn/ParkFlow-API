using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Files.Commands.UploadOrcrDocument;

public class UploadOrcrDocumentHandler : IRequestHandler<UploadOrcrDocumentCommand, Result<UploadFileResponse>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IUserContext _userContext;

    public UploadOrcrDocumentHandler(
        ICorSubmissionRepository corSubmissionRepository,
        ICloudinaryService cloudinaryService,
        IUserContext userContext)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _cloudinaryService = cloudinaryService;
        _userContext = userContext;
    }

    public async Task<Result<UploadFileResponse>> Handle(UploadOrcrDocumentCommand request, CancellationToken cancellationToken)
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

            if (corSubmission == null)
            {
                return Result<UploadFileResponse>.Failure("COR submission record not found.", ErrorCode.NotFound);
            }

            var isPdf = request.File.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            var (secureUrl, publicId) = isPdf
                ? await _cloudinaryService.UploadPdfAsync(request.File, "parkflow/orcr")
                : await _cloudinaryService.UploadImageAsync(request.File, "parkflow/orcr");

            corSubmission.UpdateSubmission(null, null, null, orcrDocumentUrl: secureUrl);
            await _corSubmissionRepository.UpdateCorSubmissionAsync(corSubmission);

            var response = new UploadFileResponse(secureUrl, publicId);
            return Result<UploadFileResponse>.Success(response, "OR/CR document updated successfully.");
        }
        catch (Exception ex)
        {
            return Result<UploadFileResponse>.Failure($"OR/CR document upload failed: {ex.Message}", ErrorCode.ServerError);
        }
    }
}
