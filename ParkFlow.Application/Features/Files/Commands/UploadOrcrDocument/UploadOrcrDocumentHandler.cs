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

namespace ParkFlow.Application.Features.Files.Commands.UploadOrcrDocument;

public class UploadOrcrDocumentHandler : IRequestHandler<UploadOrcrDocumentCommand, Result<UploadFileResponse>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IUserContext _userContext;
    private readonly IValidator<UploadOrcrDocumentCommand>? _validator;
    private readonly IVehicleRepository? _vehicleRepository;

    public UploadOrcrDocumentHandler(
        ICorSubmissionRepository corSubmissionRepository,
        ICloudinaryService cloudinaryService,
        IUserContext userContext,
        IValidator<UploadOrcrDocumentCommand>? validator = null,
        IVehicleRepository? vehicleRepository = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _cloudinaryService = cloudinaryService;
        _userContext = userContext;
        _validator = validator;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Result<UploadFileResponse>> Handle(UploadOrcrDocumentCommand request, CancellationToken cancellationToken)
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
            var isPdf = request.File.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            var (secureUrl, publicId) = isPdf
                ? await _cloudinaryService.UploadPdfAsync(request.File, "parkflow/orcr")
                : await _cloudinaryService.UploadImageAsync(request.File, "parkflow/orcr");

            if (request.CorSubmissionId.HasValue && request.CorSubmissionId.Value != Guid.Empty)
            {
                var corSubmission = await _corSubmissionRepository.GetCorSubmissionAsync(request.CorSubmissionId.Value);
                if (corSubmission != null)
                {
                    corSubmission.UpdateSubmission(null, null, null, orcrDocumentUrl: secureUrl);
                    await _corSubmissionRepository.UpdateCorSubmissionAsync(corSubmission);
                }
            }

            var response = new UploadFileResponse(secureUrl, publicId);
            return Result<UploadFileResponse>.Success(response, "OR/CR document uploaded successfully.");
        }
        catch (Exception ex)
        {
            return Result<UploadFileResponse>.Failure($"OR/CR document upload failed: {ex.Message}", ErrorCode.BadRequest);
        }
    }
}
