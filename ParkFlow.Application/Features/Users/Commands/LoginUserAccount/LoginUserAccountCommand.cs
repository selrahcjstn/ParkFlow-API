using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.DTOs;

namespace ParkFlow.Application.Features.Users.Commands.LoginUserAccount;

public record LoginUserAccountCommand(
    string Email,
    string Password) : IRequest<Result<AuthResponse>>;