using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingStudent;

public record UpdateOnboardingStudentCommand(
    Guid UserId,
    string StudentNumber,
    string Course,
    string Section,
    int YearLevel) : IRequest<Result<Guid>>;
