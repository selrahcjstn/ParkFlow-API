using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Interfaces;
using System.Linq;

namespace ParkFlow.Application.Features.Files.Commands.UploadProfilePicture;

public class UploadProfilePictureHandler : IRequestHandler<UploadProfilePictureCommand, Result<UploadFileResponse>>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IValidator<UploadProfilePictureCommand>? _validator;

    public UploadProfilePictureHandler(
        IUserProfileRepository userProfileRepository,
        ICloudinaryService cloudinaryService,
        IValidator<UploadProfilePictureCommand>? validator = null)
    {
        _userProfileRepository = userProfileRepository;
        _cloudinaryService = cloudinaryService;
        _validator = validator;
    }

    public async Task<Result<UploadFileResponse>> Handle(UploadProfilePictureCommand request, CancellationToken cancellationToken)
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
            var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
            if (profile == null)
            {
                return Result<UploadFileResponse>.Failure("User profile not found.", ErrorCode.NotFound);
            }

            // Auto-delete previous image from Cloudinary if it exists in the database
            if (!string.IsNullOrWhiteSpace(profile.ProfilePictureUrl))
            {
                var previousPublicId = CloudinaryUrlParser.ExtractPublicId(profile.ProfilePictureUrl);
                if (!string.IsNullOrWhiteSpace(previousPublicId))
                {
                    try
                    {
                        await _cloudinaryService.DeleteFileAsync(previousPublicId, isImage: true);
                    }
                    catch
                    {
 
                    }
                }
            }

            var (secureUrl, publicId) = await _cloudinaryService.UploadImageAsync(request.File, "parkflow/profiles");

            profile.UpdateProfile(null, null, null, secureUrl);
            await _userProfileRepository.UpdateAsync(profile);

            var response = new UploadFileResponse(secureUrl, publicId);
            return Result<UploadFileResponse>.Success(response, "Profile picture updated successfully.");
        }
        catch (Exception ex)
        {
            return Result<UploadFileResponse>.Failure($"Profile picture upload failed: {ex.Message}", ErrorCode.ServerError);
        }
    }
}
