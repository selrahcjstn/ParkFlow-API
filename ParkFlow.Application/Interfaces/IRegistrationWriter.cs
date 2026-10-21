using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Auth.Registration;

namespace ParkFlow.Application.Interfaces;

public interface IRegistrationWriter
{
    Task<Result<UserAccount>> SaveAsync(CompleteRegistration registration, string passwordHash, string qrHash, CancellationToken ct);
}
