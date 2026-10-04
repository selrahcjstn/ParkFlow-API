using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Features.Schedules.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Schedules.Queries.GetParkingSchedule;

public class GetParkingScheduleHandler : IRequestHandler<GetParkingScheduleQuery, Result<IEnumerable<ParkingScheduleResponseDto>>>
{
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly ICacheService? _cacheService;

    public GetParkingScheduleHandler(
        IParkingScheduleRepository parkingScheduleRepository,
        ICacheService? cacheService = null)
    {
        _parkingScheduleRepository = parkingScheduleRepository;
        _cacheService = cacheService;
    }

    public async Task<Result<IEnumerable<ParkingScheduleResponseDto>>> Handle(GetParkingScheduleQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.UserSchedules(request.UserId);

        if (_cacheService != null && request.UserId != Guid.Empty)
        {
            var cached = await _cacheService.GetAsync<List<ParkingScheduleResponseDto>>(cacheKey, cancellationToken);
            if (cached != null)
            {
                return Result<IEnumerable<ParkingScheduleResponseDto>>.Success(cached, "Parking schedules retrieved successfully.");
            }
        }

        var schedules = await _parkingScheduleRepository.GetByUserIdAsync(request.UserId);

        var dtoList = schedules.Select(s => new ParkingScheduleResponseDto
        {
            AcademicTerm = s.CorSubmission?.AcademicTerm ?? string.Empty,
            DayOfWeek = s.DayOfWeek,
            StartTime = s.StartTime,
            EndTime = s.EndTime
        }).ToList();

        if (_cacheService != null && request.UserId != Guid.Empty)
        {
            await _cacheService.SetAsync(cacheKey, dtoList, TimeSpan.FromMinutes(5), cancellationToken);
        }

        return Result<IEnumerable<ParkingScheduleResponseDto>>.Success(dtoList, "Parking schedules retrieved successfully.");
    }
}
