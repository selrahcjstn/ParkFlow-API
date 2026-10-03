using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Users.Commands.UpdateUserAccountAdmin;

public class UpdateUserAccountAdminHandler : IRequestHandler<UpdateUserAccountAdminCommand, Result<Guid>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IPasswordHasher _passwordHasher;

    public UpdateUserAccountAdminHandler(
        IUserAccountRepository userAccountRepository,
        IUserProfileRepository userProfileRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IGuardRepository guardRepository,
        IAdminRepository adminRepository,
        IPasswordHasher passwordHasher)
    {
        _userAccountRepository = userAccountRepository;
        _userProfileRepository = userProfileRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _guardRepository = guardRepository;
        _adminRepository = adminRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<Guid>> Handle(UpdateUserAccountAdminCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userAccountRepository.GetByIdAsync(request.UserId);
            if (user == null)
                return Result<Guid>.Failure("User account not found.", ErrorCode.NotFound);

            // 1. Phone Number
            if (!string.IsNullOrWhiteSpace(request.Request.PhoneNumber))
            {
                user.UpdatePhoneNumber(request.Request.PhoneNumber.Trim());
            }

            // 2. Status
            if (!string.IsNullOrWhiteSpace(request.Request.Status))
            {
                if (Enum.TryParse<AccountStatus>(request.Request.Status.Trim(), true, out var status))
                {
                    user.UpdateStatus(status);
                }
            }

            // 3. Email Update
            if (!string.IsNullOrWhiteSpace(request.Request.Email))
            {
                var normalizedEmail = request.Request.Email.Trim().ToLower();
                var currentEmail = user.PrimaryEmail?.Trim().ToLower();
                if (!string.Equals(normalizedEmail, currentEmail, StringComparison.OrdinalIgnoreCase))
                {
                    var exists = await _userAccountRepository.EmailExistsAsync(normalizedEmail, user.Id);
                    if (exists)
                    {
                        return Result<Guid>.Failure("Email address is already in use by another user.", ErrorCode.Conflict);
                    }

                    var primaryIdentity = user.AuthIdentities.FirstOrDefault(i => i.IsPrimary)
                        ?? user.AuthIdentities.FirstOrDefault(i => i.Email != null);

                    if (primaryIdentity != null)
                    {
                        primaryIdentity.UpdateEmail(normalizedEmail);
                    }
                    else
                    {
                        user.AuthIdentities.Add(AuthIdentity.CreateManual(user.Id, normalizedEmail, user.PasswordHash ?? string.Empty, true));
                    }
                }
            }

            // 4. Password Override (Optional)
            if (!string.IsNullOrWhiteSpace(request.Request.Password))
            {
                var newPassword = request.Request.Password.Trim();
                if (newPassword.Length < 6)
                {
                    return Result<Guid>.Failure("Password must be at least 6 characters.", ErrorCode.BadRequest);
                }

                var passwordHash = _passwordHasher.HashPassword(newPassword);
                user.UpdatePassword(passwordHash);

                var manualIdentity = user.AuthIdentities.FirstOrDefault(i => i.Provider == AuthProvider.Manual);
                if (manualIdentity != null)
                {
                    manualIdentity.UpdatePasswordHash(passwordHash);
                }
                else
                {
                    var email = user.PrimaryEmail ?? request.Request.Email?.Trim() ?? string.Empty;
                    var existingWithEmail = user.AuthIdentities.FirstOrDefault(i => i.Email != null && i.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
                    if (existingWithEmail != null)
                    {
                        existingWithEmail.UpdatePasswordHash(passwordHash);
                    }
                    else
                    {
                        var hasPrimary = user.AuthIdentities.Any(i => i.IsPrimary);
                        user.AuthIdentities.Add(AuthIdentity.CreateManual(user.Id, email, passwordHash, !hasPrimary));
                    }
                }

                user.PasswordHistories.Add(new PasswordHistory(user.Id, passwordHash));
            }

            // 5. User Profile
            var photoUrl = request.Request.PhotoUrl;
            if (!string.IsNullOrWhiteSpace(photoUrl) && photoUrl.Length > 2048)
            {
                // Prevent database character varying(2048) overflow if raw data URL was passed directly
                photoUrl = user.UserProfile?.ProfilePictureUrl;
            }

            var profile = user.UserProfile;
            if (profile == null)
            {
                profile = new UserProfile(
                    user.Id,
                    request.Request.FirstName?.Trim() ?? string.Empty,
                    request.Request.LastName?.Trim() ?? string.Empty,
                    request.Request.MiddleName?.Trim(),
                    photoUrl
                );
                await _userProfileRepository.AddAsync(profile);
                user.UserProfile = profile;
            }
            else
            {
                profile.UpdateProfile(
                    request.Request.FirstName?.Trim(),
                    request.Request.LastName?.Trim(),
                    request.Request.MiddleName?.Trim(),
                    photoUrl
                );
                await _userProfileRepository.UpdateAsync(profile);
            }

            // 6. Role-Specific Details & Role Switching
            var role = request.Request.Role?.Trim();
            if (!string.IsNullOrWhiteSpace(role))
            {
                if (string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase))
                {
                    // Remove non-student roles if previously assigned
                    if (profile.Personnel != null)
                    {
                        await _personnelRepository.DeleteAsync(profile.Personnel);
                        profile.Personnel = null;
                    }
                    if (profile.Guard != null)
                    {
                        await _guardRepository.DeleteAsync(profile.Guard);
                        profile.Guard = null;
                    }
                    if (profile.Admin != null)
                    {
                        await _adminRepository.DeleteAsync(profile.Admin);
                        profile.Admin = null;
                    }

                    var sReq = request.Request.Student;
                    var sNum = sReq?.StudentNumber?.Trim() ?? string.Empty;
                    var sCourse = sReq?.Course?.Trim() ?? string.Empty;
                    var sSection = sReq?.Section?.Trim() ?? "A";
                    var sYear = sReq?.YearLevel ?? 1;

                    if (profile.Student != null)
                    {
                        profile.Student.UpdateDetails(sNum, sCourse, sSection, sYear);
                        await _studentRepository.UpdateAsync(profile.Student);
                    }
                    else
                    {
                        var newStudent = new Student(profile.Id, sNum, sCourse, sSection, sYear);
                        await _studentRepository.AddAsync(newStudent);
                        profile.Student = newStudent;
                    }
                }
                else if (string.Equals(role, "UniversityStaff", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(role, "NonAcademicPersonnel", StringComparison.OrdinalIgnoreCase))
                {
                    // Remove non-personnel roles if previously assigned
                    if (profile.Student != null)
                    {
                        await _studentRepository.DeleteAsync(profile.Student);
                        profile.Student = null;
                    }
                    if (profile.Guard != null)
                    {
                        await _guardRepository.DeleteAsync(profile.Guard);
                        profile.Guard = null;
                    }
                    if (profile.Admin != null)
                    {
                        await _adminRepository.DeleteAsync(profile.Admin);
                        profile.Admin = null;
                    }

                    var pRole = string.Equals(role, "NonAcademicPersonnel", StringComparison.OrdinalIgnoreCase)
                        ? Roles.NonAcademicPersonnel
                        : Roles.UniversityStaff;

                    var pReq = request.Request.Personnel;
                    var pIdCard = pReq?.IdCardNumber?.Trim() ?? string.Empty;
                    var pDept = pReq?.Department?.Trim() ?? string.Empty;

                    if (profile.Personnel != null)
                    {
                        profile.Personnel.UpdateDetails(pIdCard, pDept, pRole);
                        await _personnelRepository.UpdateAsync(profile.Personnel);
                    }
                    else
                    {
                        var newPersonnel = new Personnel(profile.Id, pIdCard, pDept, pRole);
                        await _personnelRepository.AddAsync(newPersonnel);
                        profile.Personnel = newPersonnel;
                    }
                }
                else if (string.Equals(role, "Guard", StringComparison.OrdinalIgnoreCase))
                {
                    // Remove non-guard roles if previously assigned
                    if (profile.Student != null)
                    {
                        await _studentRepository.DeleteAsync(profile.Student);
                        profile.Student = null;
                    }
                    if (profile.Personnel != null)
                    {
                        await _personnelRepository.DeleteAsync(profile.Personnel);
                        profile.Personnel = null;
                    }
                    if (profile.Admin != null)
                    {
                        await _adminRepository.DeleteAsync(profile.Admin);
                        profile.Admin = null;
                    }

                    var gReq = request.Request.Guard;
                    var gGate = gReq?.AssignedGate ?? 1;

                    if (profile.Guard != null)
                    {
                        profile.Guard.ChangeAssignedGate(gGate);
                        await _guardRepository.UpdateAsync(profile.Guard);
                    }
                    else
                    {
                        var newGuard = new Guard(profile, gGate);
                        await _guardRepository.AddAsync(newGuard);
                        profile.Guard = newGuard;
                    }
                }
                else if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    // Remove non-admin roles if previously assigned
                    if (profile.Student != null)
                    {
                        await _studentRepository.DeleteAsync(profile.Student);
                        profile.Student = null;
                    }
                    if (profile.Personnel != null)
                    {
                        await _personnelRepository.DeleteAsync(profile.Personnel);
                        profile.Personnel = null;
                    }
                    if (profile.Guard != null)
                    {
                        await _guardRepository.DeleteAsync(profile.Guard);
                        profile.Guard = null;
                    }

                    if (profile.Admin == null)
                    {
                        var newAdmin = new Admin(profile, RoleLevel.Admin);
                        await _adminRepository.AddAsync(newAdmin);
                        profile.Admin = newAdmin;
                    }
                }
            }

            await _userAccountRepository.UpdateAsync(user);

            return Result<Guid>.Success(user.Id, "User account successfully updated.");
        }
        catch (Exception ex)
        {
            return Result<Guid>.Failure($"Failed to update user account profile: {ex.Message}", ErrorCode.BadRequest);
        }
    }
}
