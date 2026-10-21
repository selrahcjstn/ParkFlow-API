using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Cor.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Application.Features.ParkingLogs.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Cor.Commands.UpdateCorSchedules;

public class UpdateCorSchedulesHandler : IRequestHandler<UpdateCorSchedulesCommand, Result<bool>>
{
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IParkingLogRepository? _parkingLogRepository;
    private readonly IUserAccountRepository? _userAccountRepository;
    private readonly IVehicleRepository? _vehicleRepository;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    private readonly ICacheService? _cacheService;

    public UpdateCorSchedulesHandler(
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IParkingLogRepository? parkingLogRepository = null,
        IUserAccountRepository? userAccountRepository = null,
        IVehicleRepository? vehicleRepository = null,
        ISignalRNotificationSender? signalRNotificationSender = null,
        ICacheService? cacheService = null)
    {
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _parkingLogRepository = parkingLogRepository;
        _userAccountRepository = userAccountRepository;
        _vehicleRepository = vehicleRepository;
        _signalRNotificationSender = signalRNotificationSender;
        _cacheService = cacheService;
    }

    public Task<Result<bool>> Handle(UpdateCorSchedulesCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(Result<bool>.Failure("Schedules are read-only. Students must confirm the extracted COR schedule or upload a clearer COR.", ErrorCode.Forbidden));
}
