using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Features.Schedules.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Schedules.Queries.GetParkingSchedule;

public class GetParkingScheduleHandler : IRequestHandler<GetParkingScheduleQuery, Result<IEnumerable<ParkingScheduleResponseDto>>>
{
    private readonly IParkingScheduleRepository _parkingScheduleRepository;

    public GetParkingScheduleHandler(IParkingScheduleRepository parkingScheduleRepository)
    {
        _parkingScheduleRepository = parkingScheduleRepository;
    }

    public async Task<Result<IEnumerable<ParkingScheduleResponseDto>>> Handle(GetParkingScheduleQuery request, CancellationToken cancellationToken)
    {
        var schedules = await _parkingScheduleRepository.GetByUserIdAsync(request.UserId);
        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);

        var dtoList = schedules.Select(s => new ParkingScheduleResponseDto
        {
            AcademicTerm = s.CorSubmission?.AcademicTerm ?? string.Empty,
            DayOfWeek = s.DayOfWeek,
            StartTime = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesNow, s.StartTime),
            EndTime = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesNow, s.EndTime)
        }).ToList();

        return Result<IEnumerable<ParkingScheduleResponseDto>>.Success(dtoList, "Parking schedules retrieved successfully.");
    }
}
