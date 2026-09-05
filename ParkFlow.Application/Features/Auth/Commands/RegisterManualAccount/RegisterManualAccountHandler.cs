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
    private readonly ICorSubmissionRepository? _corSubmissionRepository;
    private readonly IParkingScheduleRepository? _parkingScheduleRepository;
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
        ICorSubmissionRepository? corSubmissionRepository = null,
        IParkingScheduleRepository? parkingScheduleRepository = null,
        IEmailService? emailService = null)
    {
        _userAccountRepository = userAccountRepository;
        _authIdentityRepository = authIdentityRepository;
        _userProfileRepository = userProfileRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _guardRepository = guardRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
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
            user.UpdateOnboardingStep(OnboardingStep.Profile);
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

                var emailSubject = "Welcome to ParkFlow - Your BulSU Account Credentials";
                var emailBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background-color:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f1f5f9;padding:40px 16px;'>
    <tr><td align='center'>
      <table width='600' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.08);border:1px solid #e2e8f0;'>
        <!-- BulSU Maroon & Dark Emerald Header -->
        <tr>
          <td style='background:linear-gradient(135deg, #7f1d1d 0%, #0f172a 60%, #0f766e 100%);border-top:4px solid #f59e0b;padding:36px 40px;text-align:center;'>
            <div style='display:inline-block;padding:4px 14px;background:rgba(245,158,11,0.18);border:1px solid rgba(245,158,11,0.4);border-radius:20px;color:#fbbf24;font-size:11px;font-weight:800;letter-spacing:2px;text-transform:uppercase;margin-bottom:12px;'>
              BULACAN STATE UNIVERSITY
            </div>
            <h1 style='color:#ffffff;font-size:24px;font-weight:800;margin:0;letter-spacing:-0.5px;'>ParkFlow Account Credentials</h1>
            <p style='color:rgba(255,255,255,0.85);font-size:13px;margin:6px 0 0;'>Official Campus Parking & Access Management Portal</p>
          </td>
        </tr>

        <!-- Body Content -->
        <tr>
          <td style='padding:36px 40px;'>
            <p style='font-size:15px;line-height:1.6;color:#1e293b;margin:0 0 16px;'>Hello <strong>{fullName}</strong>,</p>
            <p style='font-size:14px;line-height:1.6;color:#475569;margin:0 0 24px;'>
              Your <strong>{roleDisplayName}</strong> account for the BulSU ParkFlow campus parking system has been successfully registered by the administration.
            </p>

            <!-- Credential Box -->
            <div style='background-color:#f8fafc;border-left:4px solid #10b981;border-radius:8px;padding:20px 24px;margin:24px 0;border-top:1px solid #e2e8f0;border-right:1px solid #e2e8f0;border-bottom:1px solid #e2e8f0;'>
              <div style='font-size:11px;font-weight:800;color:#64748b;text-transform:uppercase;letter-spacing:1px;margin-bottom:12px;'>Official Account Credentials</div>
              <div style='font-size:14px;color:#334155;margin-bottom:10px;'>
                <strong style='color:#0f172a;'>Registered Email:</strong> <span style='font-family:monospace;font-weight:700;color:#0f766e;'>{normalizedEmail}</span>
              </div>
              <div style='font-size:14px;color:#334155;'>
                <strong style='color:#0f172a;'>Temporary Password:</strong> <code style='background-color:#e2e8f0;padding:4px 10px;border-radius:6px;font-family:monospace;font-size:16px;font-weight:800;color:#0f172a;letter-spacing:1px;'>{request.Password}</code>
              </div>
            </div>

            <p style='font-size:13px;line-height:1.6;color:#64748b;margin:0 0 24px;'>
              For security, please log in to the <strong>ParkFlow Mobile App</strong> using these credentials, set up your vehicle profile, and update your account password under Settings.
            </p>

            <div style='text-align:center;margin-top:28px;padding-top:20px;border-top:1px solid #e2e8f0;'>
              <p style='font-size:12px;color:#94a3b8;margin:0;'>Bulacan State University • Office of Security & Safety</p>
            </div>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style='background-color:#f8fafc;padding:20px 40px;text-align:center;border-top:1px solid #e2e8f0;'>
            <p style='font-size:11px;color:#94a3b8;margin:0;'>© {DateTime.UtcNow.Year} Bulacan State University ParkFlow System. All rights reserved.</p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";

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
