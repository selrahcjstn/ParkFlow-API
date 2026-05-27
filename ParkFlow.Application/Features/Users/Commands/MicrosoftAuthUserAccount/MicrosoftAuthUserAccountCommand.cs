using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.MicrosoftAuthUserAccount;

public record MicrosoftAuthUserAccountCommand(
    string ExternalProviderId,
    string Email,
    string? FirstName,
    string? LastName,
    string? DisplayName
) : IRequest<Result<MicrosoftAuthResultDto>>;

public record MicrosoftAuthResultDto(
    Guid UserId,
    string Email,
    string AuthProvider,
    string ExternalProviderId,
    string Token,
    bool IsNewAccount,
    OnboardingStep CurrentOnboardingStep,
    string? FirstName,
    string? LastName,
    string? DisplayName
);
