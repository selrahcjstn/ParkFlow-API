using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.LoginUserAccount;

public class LoginUserAccountHandler : IRequestHandler<LoginUserAccountCommand, Result<AuthResponse>>
{
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public LoginUserAccountHandler(
        IAuthIdentityRepository authIdentityRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _authIdentityRepository = authIdentityRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    public async Task<Result<AuthResponse>> Handle(
        LoginUserAccountCommand request,
        CancellationToken cancellationToken)
    {
        var identity = await _authIdentityRepository.GetByEmailAsync(request.Email);

        if (identity == null ||
            identity.Provider != AuthProvider.Manual ||
            string.IsNullOrWhiteSpace(identity.PasswordHash))
        {
            return Result<AuthResponse>.Failure("Invalid email or password.", ErrorCode.Unauthorized);
        }

        var user = identity.UserAccount;

        if (user == null)
        {
            return Result<AuthResponse>.Failure("Invalid email or password.", ErrorCode.Unauthorized);
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(identity.PasswordHash, request.Password);

        if (!isPasswordValid)
        {
            return Result<AuthResponse>.Failure("Invalid email or password.", ErrorCode.Unauthorized);
        }

        var profile = user.UserProfile;

        if (profile == null)
        {
            return Result<AuthResponse>.Failure("Profile missing.", ErrorCode.Unauthorized);
        }

        string? profileType =
            profile.Student != null ? "student" :
            profile.Personnel != null ? "personnel" :
            profile.Guard != null ? "guard" :
            null;

        if (profileType == null)
        {
            return Result<AuthResponse>.Failure("Invalid profile type.", ErrorCode.Unauthorized);
        }

        var token = _jwtService.GenerateToken(user, profileType);

        var isNewAccount = user.OnboardingStep != OnboardingStep.Done;

        return Result<AuthResponse>.Success(
            new AuthResponse(token, isNewAccount),
            "Login successful.");
    }
}