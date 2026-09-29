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

namespace ParkFlow.Application.Features.Users.Queries.GetUsersList;

public class GetUsersListHandler : IRequestHandler<GetUsersListQuery, Result<IEnumerable<UserWithDetailsDto>>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;

    public GetUsersListHandler(
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

    public async Task<Result<IEnumerable<UserWithDetailsDto>>> Handle(GetUsersListQuery request, CancellationToken cancellationToken)
    {
        var users = await _userAccountRepository.ListAllAsync();
        var admins = await _adminRepository.ListAllAsync();
        var adminProfileIds = admins.Select(a => a.UserProfileId).ToHashSet();

        var ownerIds = users.Select(u => u.Id).ToList();
        var vehicles = await _vehicleRepository.GetByOwnerIdsAsync(ownerIds);
        var vehiclesByOwner = vehicles.GroupBy(v => v.OwnerId).ToDictionary(g => g.Key, g => g.ToList());

        var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
        var latestCorByUser = corSubmissions
            .GroupBy(c => c.UserAccountId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CreatedAt).First());

        var dtos = new List<UserWithDetailsDto>();

        foreach (var user in users)
        {
            var userVehicles = vehiclesByOwner.TryGetValue(user.Id, out var vList)
                ? vList.Select(v => new UserVehicleDto(v.PlateNumber, v.Brand, v.VehicleType.ToString(), v.IsPrimary)).ToList()
                : new List<UserVehicleDto>();

            var profile = user.UserProfile;
            bool isAdmin = profile != null && adminProfileIds.Contains(profile.Id);

            string roleStr = "Student";
            if (isAdmin) roleStr = "Admin";
            else if (profile?.Guard != null) roleStr = "Guard";
            else if (profile?.Student != null) roleStr = "Student";
            else if (profile?.Personnel != null)
            {
                var email = user.PrimaryEmail ?? "";
                var idCard = profile.Personnel.IdCardNumber ?? "";
                if (email.Contains("faculty", StringComparison.OrdinalIgnoreCase) || idCard.StartsWith("FAC", StringComparison.OrdinalIgnoreCase))
                {
                    roleStr = "UniversityStaff";
                }
                else
                {
                    roleStr = "NonAcademicPersonnel";
                }
            }

            string corStatusStr = "NotSubmitted";
            if (latestCorByUser.TryGetValue(user.Id, out var latestCor))
            {
                corStatusStr = latestCor.VerificationStatus.ToString();
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
                ? $"{profile.FirstName} {profile.LastName}"
                : "System User";

            dtos.Add(new UserWithDetailsDto(
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
            ));
        }

        return Result<IEnumerable<UserWithDetailsDto>>.Success(dtos, "Users list retrieved successfully.");
    }
}
