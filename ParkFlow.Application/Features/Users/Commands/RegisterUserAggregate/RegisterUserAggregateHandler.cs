using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Users.Commands.RegisterUserAggregate;

public class RegisterUserAggregateHandler : IRequestHandler<RegisterUserAggregateCommand, Result<RegisterResultDto>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IQrCodeService _qrCodeService;

    public RegisterUserAggregateHandler(
        IUserAccountRepository userAccountRepository,
        IUserProfileRepository userProfileRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IVehicleRepository vehicleRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IGuardRepository guardRepository,
        IPasswordHasher passwordHasher,
        IQrCodeService qrCodeService)
    {
        _userAccountRepository = userAccountRepository;
        _userProfileRepository = userProfileRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _vehicleRepository = vehicleRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _guardRepository = guardRepository;
        _passwordHasher = passwordHasher;
        _qrCodeService = qrCodeService;
    }

    public async Task<Result<RegisterResultDto>> Handle(RegisterUserAggregateCommand request, CancellationToken cancellationToken)
    {
        return Result<RegisterResultDto>.Failure("Aggregate registration is deprecated. Use onboarding endpoints.", ErrorCode.BadRequest);
    }
}
