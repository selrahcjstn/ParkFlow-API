using MediatR;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Users.Commands.LoginUserAccount;

public class LoginUserAccountHandler : IRequestHandler<LoginUserAccountCommand, string>
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
    public async Task<string> Handle(LoginUserAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);

        if (user == null)
            throw new Exception("Invalid email or password.");

        var isPasswordValid = _passwordHasher.VerifyPassword(user.PasswordHash, request.Password);

        if(!isPasswordValid)
            throw new Exception("Invalid email or password.");

        return _jwtService.GenerateToken(user);
    }
}