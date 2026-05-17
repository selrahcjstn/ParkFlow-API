using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetRecentParkingHistory;

public class GetRecentParkingHistoryHandler : IRequestHandler<GetRecentParkingHistoryQuery, Result<IEnumerable<ParkingLogActivityDto>>>
{
    private readonly IParkingLogRepository _parkingLogRepository;

    public GetRecentParkingHistoryHandler(IParkingLogRepository parkingLogRepository)
    {
        _parkingLogRepository = parkingLogRepository;
    }

    public async Task<Result<IEnumerable<ParkingLogActivityDto>>> Handle(GetRecentParkingHistoryQuery request, CancellationToken cancellationToken)
    {
        var limit = request.Limit <= 0 ? 20 : request.Limit;
        var logs = await _parkingLogRepository.GetRecentParkingLogsAsync(limit);

        var dtos = logs.Select(parkingLog =>
        {
            var ownerProfile = parkingLog.Vehicle.Owner.UserProfile;
            var student = ownerProfile.Student;
            var personnel = ownerProfile.Personnel;

            string role;
            string idNumber = string.Empty;
            string course = string.Empty;
            int yearLevel = 0;
            string section = string.Empty;
            string department = string.Empty;

            if (student != null)
            {
                role = "student";
                idNumber = student.StudentNumber;
                course = student.Course;
                yearLevel = student.YearLevel;
                section = student.Section;
            }
            else if (personnel != null)
            {
                role = "personnel";
                idNumber = personnel.IdCardNumber;
                department = personnel.Department;
            }
            else if (ownerProfile.Guard != null)
            {
                role = "guard";
            }
            else
            {
                role = "admin";
            }

            return new ParkingLogActivityDto(
                ownerProfile.FirstName,
                ownerProfile.LastName,
                role,
                parkingLog.Status.ToString(),
                idNumber,
                course,
                yearLevel,
                section,
                department,
                parkingLog.Vehicle.PlateNumber,
                parkingLog.Vehicle.Brand,
                parkingLog.EntryTime.ToString("O"),
                parkingLog.EntryTime.Date.ToString("yyyy-MM-dd"));
        });

        return Result<IEnumerable<ParkingLogActivityDto>>.Success(dtos, "Recent parking history retrieved.");
    }
}