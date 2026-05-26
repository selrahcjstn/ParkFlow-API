using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.LoginUserAccount;

public class LoginUserAccountHandler : IRequestHandler<LoginUserAccountCommand, Result<string>>
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public LoginUserAccountHandler(
        IUserAccountRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }
    public async Task<Result<string>> Handle(LoginUserAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);

        if (user == null)
            return Result<string>.Failure("Invalid email or password.", ErrorCode.Unauthorized);

        if (user.AuthProvider != AuthProvider.Manual || string.IsNullOrWhiteSpace(user.PasswordHash))
            return Result<string>.Failure("This account uses Microsoft sign-in.", ErrorCode.Unauthorized);

        var isPasswordValid = _passwordHasher.VerifyPassword(user.PasswordHash, request.Password);

        if (!isPasswordValid)
            return Result<string>.Failure("Invalid email or password.", ErrorCode.Unauthorized);

        var profile = user.UserProfile;

        if (profile == null)
            return Result<string>.Failure("Profile missing.", ErrorCode.Unauthorized);

string? profileType = null;

if (profile.Student != null)
    profileType = "student";

else if (profile.Personnel != null)
    profileType = "personnel";

else if (profile.Guard != null)
    profileType = "guard";

if (profileType == null)
{
    return Result<string>.Failure(
        "Invalid profile type.",
        ErrorCode.Unauthorized);
}

        var token = _jwtService.GenerateToken(user, profileType);

        return Result<string>.Success(token, "Login successful.");
    }
}
