using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace ParkFlow.Persistence;

public static class DatabaseStartupCheck
{
    public const string DeferredDeletionMigration = "20261027202102_RemovePaymentAndViolationHistory";

    // Read migration metadata only. Schema changes belong to a reviewed operator step.
    public static async Task EnsureReadyAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (db.Database.HasPendingModelChanges())
            throw new InvalidOperationException("The EF model differs from its migration snapshot. Review and commit a consistent migration before releasing this API. No migrations were applied.");

        string[] applied;
        try { applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray(); }
        catch (DbException ex)
        {
            throw new InvalidOperationException("Could not read the database migration history. Check database connectivity and permissions before starting the API. Startup does not apply migrations.", ex);
        }

        if (applied.Contains(DeferredDeletionMigration, StringComparer.Ordinal))
            throw new InvalidOperationException($"Migration conflict: {DeferredDeletionMigration} was applied to this database. Stop this release and review historical-data recovery with the operator; do not rewrite migration history.");

        var known = db.Database.GetMigrations().ToArray();
        var unknown = applied.Except(known, StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0)
            throw new InvalidOperationException($"The database contains migrations unknown to this API: {string.Join(", ", unknown)}. Use the matching release or review the target database before startup.");

        var pending = known.Except(applied, StringComparer.Ordinal).ToArray();
        if (pending.Length > 0)
            throw new InvalidOperationException($"Required database migrations have not been applied: {string.Join(", ", pending)}. Startup does not change the schema. Follow docs/deployment-migration-safety.md to review and apply the approved migration separately, then restart the API.");

        // History/snapshot agreement alone cannot detect an old undiscovered migration
        // or a manually removed column. Inspect metadata only, never repair at startup.
        var actual = (await db.Database.SqlQueryRaw<string>("""
            SELECT table_name || '.' || column_name AS "Value"
            FROM information_schema.columns WHERE table_schema = current_schema()
            """).ToArrayAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var missing = db.Model.GetRelationalModel().Tables
            .SelectMany(table => table.Columns.Select(column => table.Name + "." + column.Name))
            .Where(column => !actual.Contains(column)).Order().ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"Required database columns are missing despite matching migration history: {string.Join(", ", missing)}. Stop and review schema compatibility; startup does not repair or migrate data.");
    }
}
