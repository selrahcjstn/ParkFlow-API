using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Users.Commands.CreateUserAccount
{
    public class CreateUserAccountHandler
        : IRequestHandler<CreateUserAccountCommand, Result<Guid>>
    {
        private readonly IUserAccountRepository _userAccountRepository;
        private readonly IValidator<CreateUserAccountCommand> _validator;
        private readonly IPasswordHasher _passwordHasher;

        public CreateUserAccountHandler(
            IUserAccountRepository userAccountRepository,
            IValidator<CreateUserAccountCommand> validator,
            IPasswordHasher passwordHasher)
        {
            _userAccountRepository = userAccountRepository;
            _validator = validator;
            _passwordHasher = passwordHasher;
        }

        public async Task<Result<Guid>> Handle(CreateUserAccountCommand request, CancellationToken cancellationToken)
        {
            var validationResult = await _validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                return Result<Guid>.Failure(errors, ErrorCode.UserNotFound);
            }

            var hashedPassword = _passwordHasher.HashPassword(request.Password);

            var user = new UserAccount(
                request.Email,
                hashedPassword,
                request.PhoneNumber,
                request.Role
            );

            await _userAccountRepository.AddAsync(user);

            return Result<Guid>.Success(user.Id, "User account created.");
        }
    }
}