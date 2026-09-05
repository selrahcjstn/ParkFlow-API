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
    private readonly IEmailService? _emailService;

    public RegisterManualAccountHandler(
        IUserAccountRepository userAccountRepository,
        IAuthIdentityRepository authIdentityRepository,
        IUserProfileRepository userProfileRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IGuardRepository guardRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IValidator<RegisterManualAccountCommand> validator,
        IEmailService? emailService = null)
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
        _emailService = emailService;
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

        if (_emailService != null)
        {
            try
            {
                var fullName = isFullRegistration
                    ? $"{request.FirstName?.Trim()} {request.LastName?.Trim()}"
                    : normalizedEmail;

                var roleDisplayName = resolvedRole.Equals("student", StringComparison.OrdinalIgnoreCase) ? "Student" :
                                      resolvedRole.Equals("guard", StringComparison.OrdinalIgnoreCase) ? "Security Guard" :
                                      resolvedRole.Equals("personnel", StringComparison.OrdinalIgnoreCase) ? "University Staff" : "User Account";

                var emailSubject = "Welcome to ParkFlow - Your Account Credentials";
                var emailBody = $@"
                <div style='font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,sans-serif;max-width:600px;margin:0 auto;padding:24px;background-color:#f8fafc;color:#1e293b;'>
                  <div style='background-color:#ffffff;border-radius:12px;padding:32px;border:1px solid #e2e8f0;box-shadow:0 4px 6px -1px rgba(0,0,0,0.05);'>
                    <div style='text-align:center;margin-bottom:24px;'>
                      <div style='display:inline-block;padding:12px 20px;background-color:#10b981;border-radius:8px;color:#ffffff;font-weight:800;font-size:20px;letter-spacing:1px;'>
                        PARKFLOW
                      </div>
                    </div>
                    <h2 style='color:#0f172a;margin-top:0;font-size:20px;font-weight:700;text-align:center;'>Account Created Successfully</h2>
                    <p style='font-size:15px;line-height:1.6;color:#475569;'>Hello <strong>{fullName}</strong>,</p>
                    <p style='font-size:15px;line-height:1.6;color:#475569;'>Your <strong>{roleDisplayName}</strong> account for the BulSU ParkFlow campus parking system has been registered by the administration.</p>

                    <div style='background-color:#f1f5f9;border-left:4px solid #10b981;padding:16px 20px;margin:24px 0;border-radius:6px;'>
                      <div style='font-size:12px;font-weight:700;color:#64748b;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:8px;'>Your Account Login Credentials</div>
                      <div style='font-size:14px;color:#334155;margin-bottom:8px;'><strong>Registered Email:</strong> {normalizedEmail}</div>
                      <div style='font-size:14px;color:#334155;'><strong>Initial Password:</strong> <code style='background:#e2e8f0;padding:4px 8px;border-radius:4px;font-family:monospace;font-size:15px;font-weight:700;color:#0f172a;'>{request.Password}</code></div>
                    </div>

                    <p style='font-size:14px;line-height:1.6;color:#64748b;'>For security, please log in to the ParkFlow Mobile App using these credentials and update your password under Account Settings.</p>
                    <div style='margin-top:28px;padding-top:20px;border-top:1px solid #e2e8f0;text-align:center;font-size:12px;color:#94a3b8;'>
                      © {DateTime.UtcNow.Year} Bulacan State University Security Office. All rights reserved.
                    </div>
                  </div>
                </div>";

                await _emailService.SendEmailAsync(normalizedEmail, emailSubject, emailBody);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegisterManualAccount] Could not send initial credential email: {ex.Message}");
            }
        }

        return Result<string>.Success(token, "Account registered successfully.");
    }
}
