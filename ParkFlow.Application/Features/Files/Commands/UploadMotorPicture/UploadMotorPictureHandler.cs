using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Files.Commands.UploadMotorPicture;

public class UploadMotorPictureHandler : IRequestHandler<UploadMotorPictureCommand, Result<UploadFileResponse>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly ICloudinaryService _cloudinaryService;

    public UploadMotorPictureHandler(ICorSubmissionRepository corSubmissionRepository, ICloudinaryService cloudinaryService)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<Result<UploadFileResponse>> Handle(UploadMotorPictureCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var corSubmission = await _corSubmissionRepository.GetCorSubmissionAsync(request.CorSubmissionId);
            if (corSubmission == null)
            {
                return Result<UploadFileResponse>.Failure("COR submission record not found.", ErrorCode.NotFound);
            }

            if (!string.IsNullOrWhiteSpace(corSubmission.MotorPictureUrl))
            {
                var previousPublicId = CloudinaryUrlParser.ExtractPublicId(corSubmission.MotorPictureUrl);
                if (!string.IsNullOrWhiteSpace(previousPublicId))
                {
                    try
                    {
                        await _cloudinaryService.DeleteFileAsync(previousPublicId, isImage: true);
                    }
                    catch
                    {
                        // Ignore deletion failure to avoid blocking the new upload
                    }
                }
            }

            var (secureUrl, publicId) = await _cloudinaryService.UploadImageAsync(request.File, "parkflow/motor-pictures");

            corSubmission.UpdateSubmission(null, null, null, motorPictureUrl: secureUrl);
            await _corSubmissionRepository.UpdateCorSubmissionAsync(corSubmission);

            var response = new UploadFileResponse(secureUrl, publicId);
            return Result<UploadFileResponse>.Success(response, "Motor picture updated successfully.");
        }
        catch (Exception ex)
        {
            return Result<UploadFileResponse>.Failure($"Motor picture upload failed: {ex.Message}", ErrorCode.ServerError);
        }
    }
}
