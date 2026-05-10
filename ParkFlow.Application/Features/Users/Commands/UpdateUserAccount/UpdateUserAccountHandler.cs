using MediatR;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Users.Commands.UpdateUserAccount;

public class UpdateUserAccountHandler(IUserAccountRepository userAccountRepository) : IRequestHandler<UpdateUserAccountCommand, Result<Guid>>
{
    private readonly IUserAccountRepository _userAccountRepository = userAccountRepository;

    public async Task<Result<Guid>> Handle(UpdateUserAccountCommand request, CancellationToken cancellationToken)
    { 
        var user = await _userAccountRepository.GetByIdAsync(request.Id);

        if(user == null)
        {
            return Result<Guid>.Failure("User not found.", ErrorCode.UserNotFound);
        }

       user.UpdateEmail(request.Email, request.PhoneNumber, request.Role);

        await _userAccountRepository.UpdateAsync(user);

        return Result<Guid>.Success(user.Id, "User account updated");
    }

}
