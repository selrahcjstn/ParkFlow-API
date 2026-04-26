using MediatR;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.UpdateUserAccount;

public record UpdateUserAccountCommand(
    Guid Id,
    string? Email,
    string? PhoneNumber,
    Roles? Role
) : IRequest<Guid>;