using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Infrastructure.BackgroundServices;

public class OverstayMonitoringWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverstayMonitoringWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(60);

    public OverstayMonitoringWorker(IServiceScopeFactory scopeFactory, ILogger<OverstayMonitoringWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OverstayMonitoringWorker started. Polling every {Interval}s.", _pollInterval.TotalSeconds);

        // Small initial delay on startup so other critical initialization finishes first
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IOverstayNotificationService>();
                var count = await service.CheckAndSendOverstayNotificationsAsync(ct: stoppingToken);
                if (count > 0)
                {
                    _logger.LogInformation("OverstayMonitoringWorker processed and sent {Count} notification(s).", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in OverstayMonitoringWorker cycle.");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("OverstayMonitoringWorker stopped.");
    }
}
