using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ParkFlow.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PARKFLOW_MIGRATION_CONNECTION");

        if (string.IsNullOrWhiteSpace(connection))
        {
            connection = ReadDefaultConnectionFromSettings();
        }

        connection ??= "Host=localhost;Database=parkflow;Username=postgres";
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    }

    private static string? ReadDefaultConnectionFromSettings()
    {
        try
        {
            var baseDir = Directory.GetCurrentDirectory();
            var candidatePaths = new[]
            {
                Path.Combine(baseDir, "ParkFlow.API", "appsettings.Development.json"),
                Path.Combine(baseDir, "ParkFlow.API", "appsettings.json"),
                Path.Combine(baseDir, "..", "ParkFlow.API", "appsettings.Development.json"),
                Path.Combine(baseDir, "..", "ParkFlow.API", "appsettings.json"),
                Path.Combine(baseDir, "appsettings.Development.json"),
                Path.Combine(baseDir, "appsettings.json")
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    if (doc.RootElement.TryGetProperty("ConnectionStrings", out var connStrings) &&
                        connStrings.TryGetProperty("DefaultConnection", out var defaultConn))
                    {
                        var value = defaultConn.GetString();
                        if (!string.IsNullOrWhiteSpace(value)) return value;
                    }
                }
            }
        }
        catch
        {
            // Fall through to default
        }

        return null;
    }
}
