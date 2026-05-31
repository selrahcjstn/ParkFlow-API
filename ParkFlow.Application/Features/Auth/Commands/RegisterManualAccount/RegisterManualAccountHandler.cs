using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Auth.Commands.RegisterManualAccount;

public class RegisterManualAccountHandler : IRequestHandler<RegisterManualAccountCommand, Result<string>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IValidator<RegisterManualAccountCommand> _validator;

    public RegisterManualAccountHandler(
        IUserAccountRepository userAccountRepository,
        IAuthIdentityRepository authIdentityRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IValidator<RegisterManualAccountCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
        _authIdentityRepository = authIdentityRepository;
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

        var existingIdentity = await _authIdentityRepository.GetByEmailAsync(request.Email);
        if (existingIdentity != null)
            return Result<string>.Failure("Email is already linked to an account.", ErrorCode.Conflict);

        var hashedPassword = _passwordHasher.HashPassword(request.Password);
        var user = new UserAccount(hashedPassword, null);
        await _userAccountRepository.AddAsync(user);

        var identity = AuthIdentity.CreateManual(user.Id, request.Email, hashedPassword, isPrimary: true);
        await _authIdentityRepository.AddAsync(identity);
        user.AuthIdentities.Add(identity);

        var token = _jwtService.GenerateToken(user, "unassigned");

        return Result<string>.Success(token, "Account registered successfully.");
    }
}
