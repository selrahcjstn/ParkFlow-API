using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.RegisterGuard.Commands.CreateGuardAccount;

public record CreateGuardAccountCommand(
	AccountDto Account,
	ProfileDto Profile,
	int AssignedGate
) : IRequest<Result<Guid>>;

public record AccountDto(
	string Email,
	string Password,
	string PhoneNumber
);

public record ProfileDto(
	string FirstName,
	string LastName,
	string? ProfilePictureUrl
);
