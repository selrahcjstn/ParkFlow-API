using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.LoginUserAccount;

public class LoginUserAccountHandler : IRequestHandler<LoginUserAccountCommand, Result<AuthResponse>>
{
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public LoginUserAccountHandler(
        IAuthIdentityRepository authIdentityRepository,
        IUserAccountRepository userAccountRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _authIdentityRepository = authIdentityRepository;
        _userAccountRepository = userAccountRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    public async Task<Result<AuthResponse>> Handle(
        LoginUserAccountCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<AuthResponse>.Failure("Invalid email or password.", ErrorCode.Unauthorized);
        }

        var identity = await _authIdentityRepository.GetByEmailAsync(normalizedEmail);

        UserAccount? user = identity?.UserAccount;
        string? passwordHash = identity?.PasswordHash;

        if (user == null)
        {
            user = await _userAccountRepository.GetByEmailAsync(normalizedEmail);
            if (user != null)
            {
                var matchedIdentity = user.AuthIdentities.FirstOrDefault(i => i.Email != null && i.Email.Trim().Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase) && i.Provider == AuthProvider.Manual)
                                      ?? user.AuthIdentities.FirstOrDefault(i => i.Provider == AuthProvider.Manual);
                passwordHash = matchedIdentity?.PasswordHash ?? user.PasswordHash;
            }
        }

        if (user == null || (string.IsNullOrWhiteSpace(passwordHash) && string.IsNullOrWhiteSpace(user.PasswordHash)))
        {
            return Result<AuthResponse>.Failure("Invalid email or password.", ErrorCode.Unauthorized);
        }

        if (user.Status == AccountStatus.Suspended)
        {
            return Result<AuthResponse>.Failure("Your account has been suspended. Please contact the administrator.", ErrorCode.Forbidden);
        }

        var isPasswordValid = !string.IsNullOrWhiteSpace(passwordHash) && _passwordHasher.VerifyPassword(passwordHash, request.Password);

        if (!isPasswordValid && !string.IsNullOrWhiteSpace(user.PasswordHash) && user.PasswordHash != passwordHash)
        {
            isPasswordValid = _passwordHasher.VerifyPassword(user.PasswordHash, request.Password);
        }

        if (!isPasswordValid)
        {
            return Result<AuthResponse>.Failure("Invalid email or password.", ErrorCode.Unauthorized);
        }

        var profile = user.UserProfile;
        string profileType = "unassigned";

        if (profile != null)
        {
            if (profile.Admin != null)
            {
                profileType = profile.Admin.RoleLevel == RoleLevel.SuperAdmin ? "superadmin" : "admin";
            }
            else if (profile.Guard != null)
            {
                profileType = "guard";
            }
            else if (profile.Student != null)
            {
                profileType = "student";
            }
            else if (profile.Personnel != null)
            {
                profileType = "personnel";
            }
        }

        var token = _jwtService.GenerateToken(user, profileType);

        var isNewAccount = user.OnboardingStep != OnboardingStep.Done;
        var currentOnboardingStep = user.OnboardingStep;
        return Result<AuthResponse>.Success(
            new AuthResponse(token, isNewAccount, currentOnboardingStep),
            "Login successful.");
    }
}