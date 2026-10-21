using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkFlow.Application.Features.Verification;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Infrastructure.Verification;

public class UnavailableInstitutionalVerificationProvider : IInstitutionalVerificationProvider
{
    public Task<TrustedVerificationEvidence> VerifyAsync(VerificationSubject subject, CancellationToken ct) =>
        Task.FromResult(new TrustedVerificationEvidence(false, null, subject.Fingerprint));
}

public class VerificationWorker(IServiceScopeFactory scopes, IOptions<OcrOptions> options, ILogger<VerificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var limits = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IVerificationStore>();
                var record = await store.ClaimAsync(TimeSpan.FromSeconds(limits.JobTimeoutSeconds + 60), stoppingToken);
                if (record == null) { await Task.Delay(TimeSpan.FromSeconds(limits.PollSeconds), stoppingToken); continue; }
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(limits.JobTimeoutSeconds));
                var service = scope.ServiceProvider.GetRequiredService<VerificationService>();
                try
                {
                    await service.PublishAsync(record, timeout.Token);
                    await service.ProcessAsync(record, timeout.Token);
                }
                catch (Exception) when (!stoppingToken.IsCancellationRequested)
                {
                    // Exception messages/native stderr may contain sensitive content.
                    logger.LogWarning("Verification processing failed for record {RecordId}, attempt {Attempt}.", record.Id, record.Attempts);
                    await store.RetryAsync(record, limits.MaxAttempts, stoppingToken);
                    var updated = await store.FindAsync(record.Id, stoppingToken);
                    if (updated != null) await service.PublishAsync(updated, stoppingToken, updated.Status == ParkFlow.Domain.Entities.VerificationState.NeedsReview);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch
            {
                logger.LogWarning("Verification queue unavailable; retrying after polling interval.");
                await Task.Delay(TimeSpan.FromSeconds(limits.PollSeconds), stoppingToken);
            }
        }
    }
}
