using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

public class Vehicle : BaseEntity
{
    public Guid OwnerId { get; private set; }
    public UserAccount Owner { get; private set; } = null!;

    public string PlateNumber { get; private set; } = null!;
    public string Brand { get; private set; } = null!;
    public string QrCodeHash { get; private set; } = null!;
    public VehicleType VehicleType { get; private set; }

    public ICollection<ParkingLog> ParkingLogs { get; private set; } = [];

    private Vehicle() { }

    public Vehicle(
        Guid ownerId,
        string plateNumber,
        string brand,
        string qrCodeHash,
        VehicleType vehicleType)
    {
        OwnerId = ownerId;
        PlateNumber = plateNumber;
        Brand = brand;
        QrCodeHash = qrCodeHash;
        VehicleType = vehicleType;
    }
}