using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Files.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Files.Commands.UploadProfilePicture;

public class UploadProfilePictureHandler : IRequestHandler<UploadProfilePictureCommand, Result<UploadFileResponse>>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ICloudinaryService _cloudinaryService;

    public UploadProfilePictureHandler(IUserProfileRepository userProfileRepository, ICloudinaryService cloudinaryService)
    {
        _userProfileRepository = userProfileRepository;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<Result<UploadFileResponse>> Handle(UploadProfilePictureCommand request, CancellationToken cancellationToken)
    {
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

            profile.UpdateProfile(null, null, secureUrl);
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
