using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Cor.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Cor.Queries.GetMyCorSubmission;

public class GetMyCorSubmissionHandler : IRequestHandler<GetMyCorSubmissionQuery, Result<CorSubmissionDto?>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IUserContext _userContext;
    private readonly IVehicleRepository _vehicleRepository;

    public GetMyCorSubmissionHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IUserContext userContext,
        IVehicleRepository vehicleRepository)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _userContext = userContext;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Result<CorSubmissionDto?>> Handle(GetMyCorSubmissionQuery request, CancellationToken cancellationToken)
    {
        var userId = _userContext.GetUserId();
        var submission = await _corSubmissionRepository.GetLatestByUserIdAsync(userId);

        if (submission == null)
        {
            return Result<CorSubmissionDto?>.Success(null, "No COR submission found.");
        }

        var userVehicles = await _vehicleRepository.GetByOwnerIdAsync(userId);
        var vehiclesList = userVehicles.ToList();
        var primaryVehicle = vehiclesList.FirstOrDefault(v => v.IsPrimary) ?? vehiclesList.FirstOrDefault();

        var userProfile = submission.UserAccount?.UserProfile;
        var fullName = userProfile != null
            ? $"{userProfile.FirstName} {userProfile.LastName}"
            : "Student";

        var email = submission.UserAccount?.PrimaryEmail ?? "No Email";
        var vehiclePlate = primaryVehicle?.PlateNumber ?? "N/A";
        var vehicleType = primaryVehicle != null ? primaryVehicle.VehicleType.ToString() : "N/A";

        var dto = new CorSubmissionDto(
            submission.Id,
            submission.UserAccountId,
            submission.AcademicTerm,
            submission.CorDocumentUrl,
            submission.OrcrDocumentUrl,
            submission.MotorPictureUrl,
            submission.VerificationStatus,
            fullName,
            email,
            vehiclePlate,
            vehicleType,
            submission.CreatedAt);

        return Result<CorSubmissionDto?>.Success(dto, "Latest COR submission retrieved.");
    }
}
