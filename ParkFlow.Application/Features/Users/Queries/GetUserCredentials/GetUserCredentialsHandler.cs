using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.DTOs;
using ParkFlow.Application.Interfaces;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Users.Queries.GetUserCredentials;

public class GetUserCredentialsHandler : IRequestHandler<GetUserCredentialsQuery, Result<UserCredentialsDto>>
{
    private readonly IUserAccountRepository _userAccountRepository;

    public GetUserCredentialsHandler(IUserAccountRepository userAccountRepository)
    {
        _userAccountRepository = userAccountRepository;
    }

    public async Task<Result<UserCredentialsDto>> Handle(GetUserCredentialsQuery request, CancellationToken cancellationToken)
    {
        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            return Result<UserCredentialsDto>.Failure("User account not found.", ErrorCode.NotFound);
        }

        var linkedIdentities = user.AuthIdentities.Select(identity => new LinkedIdentityDto(
            identity.Provider.ToString(),
            identity.Email,
            identity.ProviderId,
            identity.IsVerified
        )).ToList();

        var dto = new UserCredentialsDto(
            user.Email,
            user.AuthProvider.ToString(),
            user.ExternalProviderId,
            linkedIdentities
        );

        return Result<UserCredentialsDto>.Success(dto, "User credentials retrieved successfully.");
    }
}
