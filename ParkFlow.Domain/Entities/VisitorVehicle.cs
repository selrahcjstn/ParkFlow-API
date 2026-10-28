using ParkFlow.Domain.Enums;

namespace ParkFlow.Domain.Entities;

public class VisitorVehicle : BaseEntity
{
    public Guid VisitorId { get; set; }
    public Visitor Visitor { get; set; } = null!;
    public string PlateNumber { get; private set; } = string.Empty;
    public string NormalizedPlateNumber { get; private set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string? VehiclePictureUrl { get; set; }
    public ICollection<VisitSession> VisitSessions { get; set; } = new List<VisitSession>();

    public static string NormalizePlate(string? plate) => (plate ?? string.Empty).Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
    private VisitorVehicle() { }
    public VisitorVehicle(Guid visitorId, string plateNumber, VehicleType vehicleType, string brand)
    {
        VisitorId = visitorId;
        PlateNumber = plateNumber.Trim().ToUpperInvariant();
        NormalizedPlateNumber = NormalizePlate(plateNumber);
        VehicleType = vehicleType;
        Brand = brand.Trim();
    }
}
