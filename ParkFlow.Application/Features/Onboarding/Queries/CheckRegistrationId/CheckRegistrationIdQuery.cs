using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Onboarding.Queries.CheckRegistrationId;

public record CheckRegistrationIdQuery(Guid UserId, string Number, string Role) : IRequest<Result<bool>>;
