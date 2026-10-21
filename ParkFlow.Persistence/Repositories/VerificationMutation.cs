using Microsoft.EntityFrameworkCore;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories;

// Commit the source edit and its durable job together. Application's MediatR hook
// subsequently dispatches notifications and coalesces the same source revision.
internal static class VerificationMutation
{
    public static Task RunAsync(AppDbContext db, VerificationType type, Guid id, Func<Task> save,
        Func<VerificationRecord?, Task>? onQueued = null, CancellationToken ct = default) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (type == VerificationType.Schedule)
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"Cor" + id}, 0))", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({type.ToString() + id}, 0))", ct);
            var store = new VerificationStore(db);
            var previous = type == VerificationType.Account ? await store.GetSubjectAsync(type, id, default) : null;
            await save();
            if (type == VerificationType.Account)
            {
                var current = await store.GetSubjectAsync(type, id, default);
                var isCampusClient = await db.UserProfiles.AnyAsync(p => p.UserAccountId == id && (p.Student != null || p.Personnel != null));
                var account = await db.UserAccounts.FindAsync(id);
                if (account != null) await db.Entry(account).ReloadAsync();
                if (isCampusClient && account?.Status == ParkFlow.Domain.Enums.AccountStatus.Active && previous?.Fingerprint != current?.Fingerprint)
                {
                    account.UpdateStatus(ParkFlow.Domain.Enums.AccountStatus.PendingVerification);
                    await db.SaveChangesAsync();
                }
                // Administrator/guard account provisioning retains its existing workflow.
                if ((isCampusClient && (previous?.Fingerprint != current?.Fingerprint || account?.Status != ParkFlow.Domain.Enums.AccountStatus.Active)) ||
                    account?.Status == ParkFlow.Domain.Enums.AccountStatus.PendingVerification)
                    await store.EnqueueAsync(type, id, CancellationToken.None);
            }
            else if (type == VerificationType.Vehicle) await store.RetireVehicleJobsAsync(id, CancellationToken.None);
            else
            {
                var record = await store.EnqueueAsync(type, id, ct);
                if (onQueued != null) await onQueued(record);
            }
            await transaction.CommitAsync(ct);
        });
}
