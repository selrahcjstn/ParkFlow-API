namespace ParkFlow.Domain.Entities;

// A single versioned settings snapshot; tariff amounts are never inferred from defaults on restart.
public class SystemSettingsDocument
{
    public int Id { get; set; } = 1;
    public string Json { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
