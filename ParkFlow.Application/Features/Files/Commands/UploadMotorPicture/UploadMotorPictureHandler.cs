using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Files.Commands.UploadMotorPicture;

public class UploadMotorPictureHandler : IRequestHandler<UploadMotorPictureCommand, Result<UploadFileResponse>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IUserContext _userContext;
    private readonly IVehicleRepository? _vehicleRepository;

    public UploadMotorPictureHandler(
        ICorSubmissionRepository corSubmissionRepository,
        ICloudinaryService cloudinaryService,
        IUserContext userContext,
        IVehicleRepository? vehicleRepository = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _cloudinaryService = cloudinaryService;
        _userContext = userContext;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Result<UploadFileResponse>> Handle(UploadMotorPictureCommand request, CancellationToken cancellationToken)
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

            var (secureUrl, publicId) = await _cloudinaryService.UploadImageAsync(request.File, "parkflow/motor-pictures");

            if (corSubmission != null)
            {
                corSubmission.UpdateSubmission(null, null, null, motorPictureUrl: secureUrl);
                await _corSubmissionRepository.UpdateCorSubmissionAsync(corSubmission);

                if (_vehicleRepository != null)
                {
                    var vehicles = await _vehicleRepository.GetByOwnerIdAsync(corSubmission.UserAccountId);
                    foreach (var v in vehicles)
                    {
                        v.UpdateDocuments(orcrDocumentUrl: null, vehiclePictureUrl: secureUrl);
                        await _vehicleRepository.UpdateAsync(v);
                    }
                }
            }

            var response = new UploadFileResponse(secureUrl, publicId);
            return Result<UploadFileResponse>.Success(response, "Vehicle picture uploaded successfully.");
        }
        catch (Exception ex)
        {
            return Result<UploadFileResponse>.Failure($"Motor picture upload failed: {ex.Message}", ErrorCode.ServerError);
        }
    }
}
