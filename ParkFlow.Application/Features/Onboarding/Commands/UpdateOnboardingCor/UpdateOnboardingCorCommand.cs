using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingCor;

public record UpdateOnboardingCorCommand(
    Guid UserId,
    string AcademicTerm,
    string CorDocumentUrl) : IRequest<Result<Guid>>;
