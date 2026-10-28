namespace ParkFlow.Domain.Entities;

// A single versioned snapshot of operational parking settings.
public class SystemSettingsDocument
{
    public int Id { get; set; } = 1;
    public string Json { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
