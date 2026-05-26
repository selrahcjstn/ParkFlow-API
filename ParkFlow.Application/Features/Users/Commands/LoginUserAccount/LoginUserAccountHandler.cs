using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.LoginUserAccount;

public class LoginUserAccountHandler : IRequestHandler<LoginUserAccountCommand, Result<string>>
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public LoginUserAccountHandler(
        IUserAccountRepository userRepository,
        IAuthIdentityRepository authIdentityRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _userRepository = userRepository;
        _authIdentityRepository = authIdentityRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }
    public async Task<Result<string>> Handle(LoginUserAccountCommand request, CancellationToken cancellationToken)
    {
        var identity = await _authIdentityRepository.GetByEmailAsync(request.Email);
        if (identity == null || identity.Provider != AuthProvider.Manual || string.IsNullOrWhiteSpace(identity.PasswordHash))
            return Result<string>.Failure("Invalid email or password.", ErrorCode.Unauthorized);

        var user = identity.UserAccount;
        if (user == null)
            return Result<string>.Failure("Invalid email or password.", ErrorCode.Unauthorized);

        var isPasswordValid = _passwordHasher.VerifyPassword(identity.PasswordHash, request.Password);

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
