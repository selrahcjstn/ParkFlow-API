namespace ParkFlow.Domain.Entities;

public class Vehicle : BaseEntity
{
    // FK
    public Guid OwnerId { get; private set; }
    public UserAccount Owner { get; private set; } = null!;


    public String PlateNumber { get; private set; } = null!;
    public String Brand { get; private set; } = null!;
    public String QrCodeHash { get; private set; } = null!;

    private Vehicle() { }

    public Vehicle(
        Guid ownerId,
        string plateNumber,
        string brand,
        string qrCodeHash)
    {
        OwnerId = ownerId;
        PlateNumber = plateNumber;
        Brand = brand;
        QrCodeHash = qrCodeHash;
    }
}
