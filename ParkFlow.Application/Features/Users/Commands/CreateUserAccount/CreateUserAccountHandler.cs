using FluentValidation;
using MediatR;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Users.Commands.CreateUserAccount
{
    public class CreateUserAccountHandler
        : IRequestHandler<CreateUserAccountCommand, Guid>
    {
        private readonly IUserAccountRepository _userAccountRepository;
        private readonly IValidator<CreateUserAccountCommand> _validator;

        public CreateUserAccountHandler(
            IUserAccountRepository userAccountRepository,
            IValidator<CreateUserAccountCommand> validator)
        {
            _userAccountRepository = userAccountRepository;
            _validator = validator;
        }

        public async Task<Guid> Handle(CreateUserAccountCommand request, CancellationToken cancellationToken)
        {
            var validationResult = await _validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                throw new Exception(errors);
            }

            var user = new UserAccount(
                request.Email,
                request.Password,
                request.PhoneNumber,
                request.Role
            );

            await _userAccountRepository.AddAsync(user);

            return user.Id;
        }
    }
}