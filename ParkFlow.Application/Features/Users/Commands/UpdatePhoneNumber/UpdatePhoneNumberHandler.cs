using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Users.Commands.UpdatePhoneNumber;

public class UpdatePhoneNumberHandler : IRequestHandler<UpdatePhoneNumberCommand, Result<Guid>>
{
    private readonly IUserAccountRepository _userAccountRepository;

    public UpdatePhoneNumberHandler(IUserAccountRepository userAccountRepository)
    {
        _userAccountRepository = userAccountRepository;
    }

    public async Task<Result<Guid>> Handle(UpdatePhoneNumberCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return Result<Guid>.Failure("Phone number is required.", ErrorCode.BadRequest);

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user == null)
            return Result<Guid>.Failure("User account not found.", ErrorCode.NotFound);

        user.UpdatePhoneNumber(request.PhoneNumber);
        await _userAccountRepository.UpdateAsync(user);

        return Result<Guid>.Success(user.Id, "Phone number updated successfully.");
    }
}
