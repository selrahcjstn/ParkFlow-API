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
    public bool IsPrimary { get; private set; }

    public string? OrcrDocumentUrl { get; private set; }
    public string? VehiclePictureUrl { get; private set; }
    public CorVerificationStatus VerificationStatus { get; private set; } = CorVerificationStatus.Pending;

    public ICollection<ParkingLog> ParkingLogs { get; private set; } = [];

    private Vehicle() { }

    public Vehicle(
        Guid ownerId,
        string plateNumber,
        string brand,
        string qrCodeHash,
        VehicleType vehicleType,
        string? orcrDocumentUrl = null,
        string? vehiclePictureUrl = null,
        CorVerificationStatus verificationStatus = CorVerificationStatus.Pending)
    {
        OwnerId = ownerId;
        PlateNumber = plateNumber;
        Brand = brand;
        QrCodeHash = qrCodeHash;
        VehicleType = vehicleType;
        IsPrimary = false;
        OrcrDocumentUrl = orcrDocumentUrl;
        VehiclePictureUrl = vehiclePictureUrl;
        VerificationStatus = verificationStatus;
    }

    public void SetPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
    }

    public void MarkAsPrimary()
    {
        IsPrimary = true;
    }

    public void UpdateDocuments(string? orcrDocumentUrl, string? vehiclePictureUrl)
    {
        OrcrDocumentUrl = orcrDocumentUrl;
        VehiclePictureUrl = vehiclePictureUrl;
    }

    public void UpdateVerificationStatus(CorVerificationStatus status)
    {
        VerificationStatus = status;
    }

    public void Update(string plateNumber, string brand, VehicleType vehicleType)
    {
        PlateNumber = plateNumber;
        Brand = brand;
        VehicleType = vehicleType;
    }

    public void Update(string plateNumber, string brand, VehicleType vehicleType, string qrCodeHash)
    {
        PlateNumber = plateNumber;
        Brand = brand;
        VehicleType = vehicleType;
        QrCodeHash = qrCodeHash;
    }
}