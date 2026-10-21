using ParkFlow.Application.Common;
namespace ParkFlow.Application.Interfaces;

public interface ISystemSettingsRepository
{
    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(SystemSettingsDto settings, CancellationToken ct = default);
}
