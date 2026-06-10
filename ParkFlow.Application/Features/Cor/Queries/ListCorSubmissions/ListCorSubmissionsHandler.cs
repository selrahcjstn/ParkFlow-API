using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Cor.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Cor.Queries.ListCorSubmissions;

public class ListCorSubmissionsHandler : IRequestHandler<ListCorSubmissionsQuery, Result<IEnumerable<CorSubmissionDto>>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IVehicleRepository _vehicleRepository;

    public ListCorSubmissionsHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IVehicleRepository vehicleRepository)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Result<IEnumerable<CorSubmissionDto>>> Handle(ListCorSubmissionsQuery request, CancellationToken cancellationToken)
    {
        var submissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
        var submissionsList = submissions.ToList();

        var userIds = submissionsList.Select(s => s.UserAccountId).Distinct().ToList();
        var vehicles = await _vehicleRepository.GetByOwnerIdsAsync(userIds);
        var vehiclesList = vehicles.ToList();

        var dtos = submissionsList.Select(s =>
        {
            var userProfile = s.UserAccount?.UserProfile;
            var fullName = userProfile != null 
                ? $"{userProfile.FirstName} {userProfile.LastName}" 
                : "Unknown Student";
            
            var email = s.UserAccount?.PrimaryEmail ?? "No Email";

            var userVehicles = vehiclesList.Where(v => v.OwnerId == s.UserAccountId).ToList();
            var primaryVehicle = userVehicles.FirstOrDefault(v => v.IsPrimary) ?? userVehicles.FirstOrDefault();

            var vehiclePlate = primaryVehicle?.PlateNumber ?? "N/A";
            var vehicleType = primaryVehicle != null ? primaryVehicle.VehicleType.ToString() : "N/A";

            return new CorSubmissionDto(
                s.Id,
                s.UserAccountId,
                s.AcademicTerm,
                s.CorDocumentUrl,
                s.VerificationStatus,
                fullName,
                email,
                vehiclePlate,
                vehicleType,
                s.CreatedAt
            );
        });

        return Result<IEnumerable<CorSubmissionDto>>.Success(dtos, "COR submissions retrieved.");
    }
}
