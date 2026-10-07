using ParkFlow.Domain.Enums;

namespace ParkFlow.Domain.Entities;

public class Visitor : BaseEntity
{
    public string FullName { get; set; } = "Visitor";
    public string? ContactNumber { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public string Brand { get; set; } = string.Empty;

    public ICollection<VisitSession> VisitSessions { get; set; } = new List<VisitSession>();

    public Visitor() { }

    public Visitor(
        string? fullName,
        string? contactNumber,
        string plateNumber,
        VehicleType vehicleType,
        string brand)
    {
        FullName = string.IsNullOrWhiteSpace(fullName) ? "Visitor" : fullName.Trim();
        ContactNumber = contactNumber?.Trim();
        PlateNumber = plateNumber.Trim().ToUpper();
        VehicleType = vehicleType;
        Brand = string.IsNullOrWhiteSpace(brand) ? "Unknown Brand" : brand.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string? fullName, string? contactNumber, VehicleType vehicleType, string? brand)
    {
        if (!string.IsNullOrWhiteSpace(fullName)) FullName = fullName.Trim();
        if (contactNumber != null) ContactNumber = contactNumber.Trim();
        VehicleType = vehicleType;
        if (!string.IsNullOrWhiteSpace(brand)) Brand = brand.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
