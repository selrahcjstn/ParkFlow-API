using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
namespace ParkFlow.Persistence.Repositories;

public class DriverDocumentRepository(AppDbContext db) : IDriverDocumentRepository
{
    public Task<bool> SubmitAsync(Guid userId, string? licenseUrl, string? photoUrl, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var key = VerificationType.Account.ToString() + userId;
            // Same account lock used by vehicle review, preventing a stale license approval.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
            var profile = await db.UserProfiles.Include(p => p.UserAccount).Include(p => p.Student).SingleOrDefaultAsync(p => p.UserAccountId == userId, ct);
            if (profile == null) return false;
            if (photoUrl != null) profile.UpdateProfile(null, null, profile.MiddleName, photoUrl);
            if (licenseUrl != null)
            {
                profile.SubmitLicense(licenseUrl);
                var vehicles = await db.Vehicles.Where(v => v.OwnerId == userId).ToListAsync(ct);
                foreach (var vehicle in vehicles) vehicle.UpdateVerificationStatus(CorVerificationStatus.Pending, null);
                if (profile.Student != null && profile.UserAccount.Status != AccountStatus.Suspended)
                    profile.UserAccount.UpdateStatus(AccountStatus.PendingVerification);
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        });
}
