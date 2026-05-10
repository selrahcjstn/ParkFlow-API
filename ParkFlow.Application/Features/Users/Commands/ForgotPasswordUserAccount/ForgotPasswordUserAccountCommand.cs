using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Users.Commands.ForgotPasswordUserAccount;

public record ForgotPasswordUserAccountCommand(string Email) : IRequest<Result<string>>;
