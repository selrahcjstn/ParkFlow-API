using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Users.Queries.GetUserById;

public class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, Result<UserWithDetailsDto>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;

    public GetUserByIdHandler(
        IUserAccountRepository userAccountRepository,
        IAdminRepository adminRepository,
        IVehicleRepository vehicleRepository,
        ICorSubmissionRepository corSubmissionRepository)
    {
        _userAccountRepository = userAccountRepository;
        _adminRepository = adminRepository;
        _vehicleRepository = vehicleRepository;
        _corSubmissionRepository = corSubmissionRepository;
    }

    public async Task<Result<UserWithDetailsDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userAccountRepository.GetByIdAsync(request.Id);
        if (user == null)
            return Result<UserWithDetailsDto>.Failure("User account not found.", ErrorCode.NotFound);

        var admins = await _adminRepository.ListAllAsync();
        var adminProfileIds = admins.Select(a => a.UserProfileId).ToHashSet();

        var vehicles = await _vehicleRepository.GetByOwnerIdAsync(user.Id);
        var userVehicles = vehicles.Select(v => new UserVehicleDto(
            v.Id,
            v.PlateNumber,
            v.Brand,
            v.VehicleType.ToString(),
            v.IsPrimary,
            v.OrcrDocumentUrl,
            v.VehiclePictureUrl,
            v.VerificationStatus.ToString(),
            v.RejectionReason
        )).ToList();

        var latestCor = await _corSubmissionRepository.GetLatestByUserIdAsync(user.Id);
        string corStatusStr = latestCor?.VerificationStatus.ToString() ?? "NotSubmitted";

        var profile = user.UserProfile;
        bool isAdmin = profile != null && adminProfileIds.Contains(profile.Id);

        string roleStr = "Student";
        if (isAdmin) roleStr = "Admin";
        else if (profile?.Guard != null) roleStr = "Guard";
        else if (profile?.Student != null) roleStr = "Student";
        else if (profile?.Personnel != null)
        {
            roleStr = profile.Personnel.Role == Roles.NonAcademicPersonnel
                ? "NonAcademicPersonnel"
                : "UniversityStaff";
        }

        var studentDto = profile?.Student != null
            ? new UserStudentDto(profile.Student.StudentNumber ?? string.Empty, profile.Student.Course ?? string.Empty, profile.Student.Section ?? string.Empty, profile.Student.YearLevel)
            : null;

        var personnelDto = profile?.Personnel != null
            ? new UserPersonnelDto(profile.Personnel.IdCardNumber ?? string.Empty, profile.Personnel.Department ?? string.Empty)
            : null;

        var guardDto = profile?.Guard != null
            ? new UserGuardDto(profile.Guard.AssignedGate)
            : null;

        var fullName = profile != null
            ? $"{profile.FirstName} {profile.LastName}".Trim()
            : "System User";

        var dto = new UserWithDetailsDto(
            user.Id,
            profile?.FirstName ?? string.Empty,
            profile?.LastName ?? string.Empty,
            profile?.MiddleName,
            fullName,
            user.PrimaryEmail ?? string.Empty,
            user.PhoneNumber ?? string.Empty,
            user.Status.ToString(),
            corStatusStr,
            user.AuthProvider.ToString(),
            roleStr,
            user.CreatedAt,
            profile?.ProfilePictureUrl,
            studentDto,
            personnelDto,
            guardDto,
            userVehicles
        );

        return Result<UserWithDetailsDto>.Success(dto, "User details retrieved successfully.");
    }
}
