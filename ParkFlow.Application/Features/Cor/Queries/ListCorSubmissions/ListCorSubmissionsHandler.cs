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
    private readonly IParkingScheduleRepository _parkingScheduleRepository;

    public ListCorSubmissionsHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IVehicleRepository vehicleRepository,
        IParkingScheduleRepository parkingScheduleRepository)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _vehicleRepository = vehicleRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
    }

    public async Task<Result<IEnumerable<CorSubmissionDto>>> Handle(ListCorSubmissionsQuery request, CancellationToken cancellationToken)
    {
        var submissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
        var submissionsList = submissions
            .Where(s => s.VerificationStatus != ParkFlow.Domain.Enums.CorVerificationStatus.NotSubmitted)
            .ToList();

        var userIds = submissionsList.Select(s => s.UserAccountId).Distinct().ToList();
        var vehicles = await _vehicleRepository.GetByOwnerIdsAsync(userIds);
        var vehiclesList = vehicles.ToList();

        var dtos = new List<CorSubmissionDto>();

        foreach (var s in submissionsList)
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

            var rawSchedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(s.Id);
            var scheduleDtos = rawSchedules.Select(sched => new CorScheduleItemDto(
                sched.DayOfWeek,
                sched.StartTime,
                sched.EndTime
            )).ToList();

            var effectiveOrcr = !string.IsNullOrWhiteSpace(s.OrcrDocumentUrl) && !s.OrcrDocumentUrl.Equals("pending", StringComparison.OrdinalIgnoreCase)
                ? s.OrcrDocumentUrl
                : (!string.IsNullOrWhiteSpace(primaryVehicle?.OrcrDocumentUrl) ? primaryVehicle!.OrcrDocumentUrl! : s.CorDocumentUrl);

            var effectiveMotor = !string.IsNullOrWhiteSpace(s.MotorPictureUrl) && !s.MotorPictureUrl.Equals("pending", StringComparison.OrdinalIgnoreCase)
                ? s.MotorPictureUrl
                : (!string.IsNullOrWhiteSpace(primaryVehicle?.VehiclePictureUrl) ? primaryVehicle!.VehiclePictureUrl! : s.CorDocumentUrl);

            dtos.Add(new CorSubmissionDto(
                s.Id,
                s.UserAccountId,
                s.AcademicTerm,
                s.CorDocumentUrl,
                effectiveOrcr,
                effectiveMotor,
                s.VerificationStatus,
                fullName,
                email,
                vehiclePlate,
                vehicleType,
                s.CreatedAt,
                scheduleDtos
            ));
        }

        return Result<IEnumerable<CorSubmissionDto>>.Success(dtos, "COR submissions retrieved.");
    }
}
