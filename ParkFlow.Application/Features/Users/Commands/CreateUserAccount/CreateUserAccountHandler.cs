using MediatR;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Users.Commands.CreateUserAccount
{
    public class CreateUserAccountHandler
        : IRequestHandler<CreateUserAccountCommand, Guid>
    {
        private readonly IUserAccountRepository _userAccountRepository;

        public CreateUserAccountHandler(IUserAccountRepository userAccountRepository)
        {
            _userAccountRepository = userAccountRepository;
        }

        public async Task<Guid> Handle(CreateUserAccountCommand request, CancellationToken cancellationToken)
        {
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