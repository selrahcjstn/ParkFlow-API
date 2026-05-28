using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Users.Commands.UpdatePhoneNumber;

public record UpdatePhoneNumberCommand(
    Guid UserId,
    string PhoneNumber
) : IRequest<Result<Guid>>;
