using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkFlow.Persistence;
using Xunit;

namespace Test.Support;

// Every fixture owns a freshly created, empty schema. No existing schema is reset.
public sealed class VisitorPostgresDatabase : IAsyncLifetime
{
    private string connection = "";
    public string Schema { get; } = "parkflow_visitors_" + Guid.NewGuid().ToString("N");
    public AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    public async Task InitializeAsync()
    {
        var value = Environment.GetEnvironmentVariable("PARKFLOW_VISITOR_TEST_CONNECTION");
        var target = new NpgsqlConnectionStringBuilder(value);
        if (target.Host is not ("localhost" or "127.0.0.1") || target.Database != "parkflow_visitor_test")
            throw new InvalidOperationException("Visitor integration fixtures require local parkflow_visitor_test; no shared or production database is allowed.");
        target.SearchPath = Schema;
        connection = target.ConnectionString;
        await using var db = Context();
        var createSchema = "CREATE SCHEMA \"" + Schema + "\"";
        await db.Database.ExecuteSqlRawAsync(createSchema);
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync()
    {
        // Retain synthetic rows and migration evidence for inspection, including failures.
        using var key = new NpgsqlConnection(connection);
        NpgsqlConnection.ClearPool(key);
        return Task.CompletedTask;
    }
}
