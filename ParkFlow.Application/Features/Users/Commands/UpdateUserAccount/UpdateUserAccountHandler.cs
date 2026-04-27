using MediatR;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Users.Commands.UpdateUserAccount;

public class UpdateUserAccountHandler(IUserAccountRepository userAccountRepository) : IRequestHandler<UpdateUserAccountCommand, Guid>
{
    private readonly IUserAccountRepository _userAccountRepository = userAccountRepository;

    public async Task<Guid> Handle(UpdateUserAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await _userAccountRepository.GetByIdAsync(request.Id);

        if(user == null)
        {
            throw new Exception("User not found.");
        }

       user.UpdateEmail(request.Email, request.PhoneNumber, request.Role);

        await _userAccountRepository.UpdateAsync(user);

        return user.Id;
    }
}
