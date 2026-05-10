using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Users.Commands.ResetPasswordUserAccount;

public record ResetPasswordUserAccountCommand(
    string Email,
    string ResetToken,
    string NewPassword) : IRequest<Result<Guid>>;
