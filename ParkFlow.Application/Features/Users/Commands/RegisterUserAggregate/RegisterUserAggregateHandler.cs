using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.RegisterUserAggregate;

public class RegisterUserAggregateHandler : IRequestHandler<RegisterUserAggregateCommand, Result<RegisterResultDto>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IQrCodeService _qrCodeService;
    private readonly IValidator<RegisterUserAggregateCommand> _validator;

    public RegisterUserAggregateHandler(
        IUserAccountRepository userAccountRepository,
        IAuthIdentityRepository authIdentityRepository,
        IUserProfileRepository userProfileRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IVehicleRepository vehicleRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IPasswordHasher passwordHasher,
        IQrCodeService qrCodeService,
        IValidator<RegisterUserAggregateCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
        _authIdentityRepository = authIdentityRepository;
        _userProfileRepository = userProfileRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _vehicleRepository = vehicleRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _passwordHasher = passwordHasher;
        _qrCodeService = qrCodeService;
        _validator = validator;
    }

    public async Task<Result<RegisterResultDto>> Handle(RegisterUserAggregateCommand request, CancellationToken cancellationToken)
    {
        // --- Validate ---
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<RegisterResultDto>.Failure(errors, ErrorCode.BadRequest);
        }

        // --- Duplicate email check ---
        var existing = await _userAccountRepository.GetByEmailAsync(request.Account.Email);
        if (existing is not null)
            return Result<RegisterResultDto>.Failure("An account with this email already exists.", ErrorCode.Conflict);

        // --- UserAccount + AuthIdentity ---
        var hashedPassword = _passwordHasher.HashPassword(request.Account.Password);
        var userAccount = new UserAccount(request.Account.Email, hashedPassword, request.Account.PhoneNumber);
        await _userAccountRepository.AddAsync(userAccount);

        var identity = AuthIdentity.CreateManual(userAccount.Id, request.Account.Email, hashedPassword);
        await _authIdentityRepository.AddAsync(identity);

        // --- UserProfile ---
        var userProfile = new UserProfile(userAccount.Id, request.Profile.FirstName, request.Profile.LastName, request.Profile.ProfilePictureUrl);
        await _userProfileRepository.AddAsync(userProfile);

        // --- Student (optional) ---
        if (request.Student is not null)
        {
            var student = new Student(userProfile.Id, request.Student.StudentNumber, request.Student.Course, request.Student.Section, request.Student.YearLevel);
            await _studentRepository.AddAsync(student);
        }

        // --- Personnel (optional) ---
        if (request.Personnel is not null)
        {
            var personnel = new Personnel(userProfile.Id, request.Personnel.IdCardNumber, request.Personnel.Department);
            await _personnelRepository.AddAsync(personnel);
        }

        // --- COR Submission (optional) ---
        Guid? submissionId = null;
        if (request.CorSubmission is not null)
        {
            var cor = new CorSubmission(userAccount.Id, request.CorSubmission.AcademicTerm, request.CorSubmission.CorDocumentUrl);
            await _corSubmissionRepository.AddCorSubmissionAsync(cor);
            submissionId = cor.Id;

            // --- Parking Schedules (optional, only if COR provided) ---
            if (request.ParkingSchedules is { Count: > 0 })
            {
                foreach (var scheduleItem in request.ParkingSchedules)
                {
                    var schedule = new ParkingSchedule(cor.Id, scheduleItem.DayOfWeek, scheduleItem.StartTime, scheduleItem.EndTime);
                    await _parkingScheduleRepository.AddAsync(schedule);
                }
            }
        }

        // --- Vehicles (optional) ---
        var vehicleResults = new List<VehicleResultDto>();
        if (request.Vehicles is { Count: > 0 })
        {
            foreach (var vehicleDto in request.Vehicles)
            {
                var qrPayload = $"{userAccount.Id}:{vehicleDto.PlateNumber}:{vehicleDto.Brand}";
                var qrBytes = _qrCodeService.GenerateQrCode(qrPayload);
                var qrCodeHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(qrBytes));

                var vehicle = new Vehicle(userAccount.Id, vehicleDto.PlateNumber, vehicleDto.Brand, qrCodeHash, vehicleDto.VehicleType);
                await _vehicleRepository.AddAsync(vehicle);

                vehicleResults.Add(new VehicleResultDto(vehicle.Id, vehicle.PlateNumber, vehicle.Brand, vehicle.QrCodeHash, vehicle.VehicleType));
            }
        }

        // --- Mark onboarding as Done since aggregate registration covers all steps ---
        userAccount.UpdateOnboardingStep(OnboardingStep.Done);
        await _userAccountRepository.UpdateAsync(userAccount);

        var resultDto = new RegisterResultDto(userAccount.Id, submissionId, vehicleResults.Count > 0 ? vehicleResults : null);
        return Result<RegisterResultDto>.Success(resultDto, "Registration completed successfully.");
    }
}
