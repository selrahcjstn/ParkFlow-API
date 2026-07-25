using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Users.Commands.DeleteUserAccount;

public class DeleteUserAccountHandler(IUserAccountRepository userAccountRepository)
    : IRequestHandler<DeleteUserAccountCommand, Result<bool>>
{
    private readonly IUserAccountRepository _userAccountRepository = userAccountRepository;

    public async Task<Result<bool>> Handle(DeleteUserAccountCommand request, CancellationToken cancellationToken)
    {
        var deleted = await _userAccountRepository.DeleteAsync(request.Id);
        if (!deleted)
        {
            return Result<bool>.Failure(false, "User not found or deletion failed.", ErrorCode.NotFound);
        }

        return Result<bool>.Success(true);
    }
}
