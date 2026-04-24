using MediatR;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.CreateUserAccount
{
    public record CreateUserAccountCommand(
            string Email,
            string Password,
            string PhoneNumber,
            Roles Role
        ) : IRequest<Guid>;
}
