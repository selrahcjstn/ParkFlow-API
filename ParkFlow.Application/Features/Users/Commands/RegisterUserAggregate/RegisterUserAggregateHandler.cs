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
    private readonly IPasswordHasher _passwordHasher;
    private readonly IQrCodeService _qrCodeService;

    public RegisterUserAggregateHandler(
        IUserAccountRepository userAccountRepository,
        IUserProfileRepository userProfileRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IVehicleRepository vehicleRepository,
        IPasswordHasher passwordHasher,
        IQrCodeService qrCodeService)
    {
        _userAccountRepository = userAccountRepository;
        _userProfileRepository = userProfileRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _vehicleRepository = vehicleRepository;
        _passwordHasher = passwordHasher;
        _qrCodeService = qrCodeService;
    }

    public async Task<Result<RegisterResultDto>> Handle(RegisterUserAggregateCommand request, CancellationToken cancellationToken)
    {
        // Basic validation (more detailed validation can be in a validator)
        if (request.Account is null)
            return Result<RegisterResultDto>.Failure("Account section is required.", ErrorCode.BadRequest);

        var existing = await _userAccountRepository.GetByEmailAsync(request.Account.Email);
        if (existing != null)
            return Result<RegisterResultDto>.Failure("User account with this email already exists.", ErrorCode.Conflict);

        try
        {
            // Create account
            var hashed = _passwordHasher.HashPassword(request.Account.Password);
            var user = new UserAccount(request.Account.Email, hashed, request.Account.PhoneNumber, request.Account.Role);
            await _userAccountRepository.AddAsync(user);

            Guid? submissionId = null;
            var vehicleIds = new List<Guid>();

            // Create profile
            if (request.Profile is not null)
            {
                var profile = new UserProfile(
                    user.Id,
                    request.Profile.IdCardNumber,
                    request.Profile.FirstName,
                    request.Profile.LastName,
                    request.Profile.ProfilePictureUrl,
                    request.Profile.Course,
                    request.Profile.Section,
                    request.Profile.YearLevel,
                    request.Profile.Office
                );

                await _userProfileRepository.AddAsync(profile);
            }

            // Create COR submission
            if (request.CorSubmission is not null)
            {
                var cor = new CorSubmission(user.Id, request.CorSubmission.AcademicTerm, request.CorSubmission.CorDocumentUrl);
                await _corSubmissionRepository.AddCorSubmissionAsync(cor);
                submissionId = cor.Id;

                // Parking schedules tied to this submission
                if (request.ParkingSchedules is not null)
                {
                    foreach (var item in request.ParkingSchedules)
                    {
                        var ps = new ParkFlow.Domain.Entities.ParkingSchedule(submissionId.Value, item.DayOfWeek, item.StartTime, item.EndTime);
                        await _parkingScheduleRepository.AddAsync(ps);
                    }
                }
            }

            // Vehicles
            if (request.Vehicles is not null)
            {
                foreach (var v in request.Vehicles)
                {
                    // generate QR bytes and convert to base64 as a stored hash/string
                    var qrBytes = _qrCodeService.GenerateQrCode(v.PlateNumber + "|" + user.Id);
                    var qrBase64 = Convert.ToBase64String(qrBytes);

                    var vehicle = new Vehicle(user.Id, v.PlateNumber, v.Brand, qrBase64);
                    await _vehicleRepository.AddAsync(vehicle);
                    vehicleIds.Add(vehicle.Id);
                }
            }

            var resultDto = new RegisterResultDto(user.Id, submissionId, vehicleIds.Any() ? vehicleIds : null);
            return Result<RegisterResultDto>.Success(resultDto, "Registered successfully.");
        }
        catch (Exception ex)
        {
            return Result<RegisterResultDto>.Failure("Registration failed: " + ex.Message, ErrorCode.BadRequest);
        }
    }
}
