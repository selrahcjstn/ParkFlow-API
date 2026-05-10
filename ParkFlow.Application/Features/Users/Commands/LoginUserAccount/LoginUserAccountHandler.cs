using MediatR;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

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
            return Result<string>.Failure("Invalid email or password.", ErrorCode.InvalidPassword);

        var isPasswordValid = _passwordHasher.VerifyPassword(user.PasswordHash, request.Password);

        if(!isPasswordValid)
            return Result<string>.Failure("Invalid email or password.", ErrorCode.InvalidPassword);

        var token = _jwtService.GenerateToken(user);
        return Result<string>.Success(token, "Login successful.");
    }
}