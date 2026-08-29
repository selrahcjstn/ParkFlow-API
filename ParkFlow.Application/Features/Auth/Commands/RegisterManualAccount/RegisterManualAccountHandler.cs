using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Auth.Commands.RegisterManualAccount;

public class RegisterManualAccountHandler : IRequestHandler<RegisterManualAccountCommand, Result<string>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IValidator<RegisterManualAccountCommand> _validator;

    public RegisterManualAccountHandler(
        IUserAccountRepository userAccountRepository,
        IAuthIdentityRepository authIdentityRepository,
        IUserProfileRepository userProfileRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IGuardRepository guardRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IValidator<RegisterManualAccountCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
        _authIdentityRepository = authIdentityRepository;
        _userProfileRepository = userProfileRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _guardRepository = guardRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _validator = validator;
    }

    public async Task<Result<string>> Handle(RegisterManualAccountCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<string>.Failure(errors, ErrorCode.BadRequest);
        }

        var normalizedEmail = request.Email.Trim().ToLower();

        var existingIdentity = await _authIdentityRepository.GetByEmailAsync(normalizedEmail);
        if (existingIdentity != null)
            return Result<string>.Failure("Email is already linked to an account.", ErrorCode.Conflict);

        var existingUser = await _userAccountRepository.GetByEmailAsync(normalizedEmail);
        if (existingUser != null)
            return Result<string>.Failure("User account with this email already exists.", ErrorCode.Conflict);

        var hashedPassword = _passwordHasher.HashPassword(request.Password);
        var phoneNumber = !string.IsNullOrWhiteSpace(request.PhoneNumber) ? request.PhoneNumber.Trim() : null;
        var user = new UserAccount(hashedPassword, phoneNumber);

        var isFullRegistration = !string.IsNullOrWhiteSpace(request.FirstName) && !string.IsNullOrWhiteSpace(request.LastName);

        if (isFullRegistration)
        {
            user.Verify();
            user.UpdateOnboardingStep(OnboardingStep.Done);
        }

        if (string.Equals(request.Status, "Suspended", StringComparison.OrdinalIgnoreCase))
        {
            user.UpdateStatus(AccountStatus.Suspended);
        }

        user.PasswordHistories.Add(new PasswordHistory(user.Id, hashedPassword));
        await _userAccountRepository.AddAsync(user);

        var identity = AuthIdentity.CreateManual(user.Id, normalizedEmail, hashedPassword, isPrimary: true);
        await _authIdentityRepository.AddAsync(identity);
        user.AuthIdentities.Add(identity);

        string resolvedRole = "unassigned";

        if (isFullRegistration)
        {
            var userProfile = new UserProfile(
                user.Id,
                request.FirstName!.Trim(),
                request.LastName!.Trim(),
                request.MiddleName?.Trim(),
                null);

            await _userProfileRepository.AddAsync(userProfile);

            var roleStr = request.Role?.Trim() ?? "";

            if (roleStr.Equals("Student", StringComparison.OrdinalIgnoreCase) || request.Student != null)
            {
                resolvedRole = "student";
                var studentNumber = request.Student?.StudentNumber?.Trim();
                if (string.IsNullOrWhiteSpace(studentNumber))
                    studentNumber = $"STU-{DateTime.UtcNow.Ticks % 1000000}";

                var course = request.Student?.Course?.Trim();
                if (string.IsNullOrWhiteSpace(course)) course = "General";

                var section = request.Student?.Section?.Trim();
                if (string.IsNullOrWhiteSpace(section)) section = "A";

                var yearLevel = request.Student?.YearLevel ?? 1;

                var student = new Student(userProfile.Id, studentNumber, course, section, yearLevel);
                await _studentRepository.AddAsync(student);
            }
            else if (roleStr.Equals("Guard", StringComparison.OrdinalIgnoreCase) || request.Guard != null)
            {
                resolvedRole = "guard";
                var assignedGate = request.Guard?.AssignedGate ?? 1;
                var guard = new Guard(userProfile, assignedGate);
                await _guardRepository.AddAsync(guard);
            }
            else if (roleStr.Equals("UniversityStaff", StringComparison.OrdinalIgnoreCase) ||
                     roleStr.Equals("NonAcademicPersonnel", StringComparison.OrdinalIgnoreCase) ||
                     roleStr.Equals("Personnel", StringComparison.OrdinalIgnoreCase) ||
                     request.Personnel != null)
            {
                resolvedRole = "personnel";
                var idCard = request.Personnel?.IdCardNumber?.Trim();
                if (string.IsNullOrWhiteSpace(idCard))
                    idCard = $"EMP-{DateTime.UtcNow.Ticks % 1000000}";

                var dept = request.Personnel?.Department?.Trim();
                if (string.IsNullOrWhiteSpace(dept))
                    dept = "General Administration";

                var personnel = new Personnel(userProfile.Id, idCard, dept);
                await _personnelRepository.AddAsync(personnel);
            }
        }

        var token = _jwtService.GenerateToken(user, resolvedRole);

        return Result<string>.Success(token, "Account registered successfully.");
    }
}
