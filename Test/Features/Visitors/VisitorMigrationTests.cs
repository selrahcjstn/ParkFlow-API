using Microsoft.EntityFrameworkCore;
using ParkFlow.Persistence;
using Xunit;

namespace Test.Features.Visitors;

public class VisitorMigrationTests
{
    [Fact]
    public void VisitorTablesMigrationIsDiscoverable()
    {
        // Enumerating migrations is local metadata inspection; no database is contacted.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        using var context = new AppDbContext(options);

        Assert.Contains("20261007163000_AddVisitorsAndVisitSessions", context.Database.GetMigrations());
    }
}
