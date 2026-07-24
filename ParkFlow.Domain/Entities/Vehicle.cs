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
    public string? Color { get; private set; }
    public string? MotorPictureUrl { get; private set; }
    public string? OrcrDocumentUrl { get; private set; }

    public ICollection<ParkingLog> ParkingLogs { get; private set; } = [];

    private Vehicle() { }

    public Vehicle(
        Guid ownerId,
        string plateNumber,
        string brand,
        string qrCodeHash,
        VehicleType vehicleType,
        string? color = null,
        string? motorPictureUrl = null,
        string? orcrDocumentUrl = null)
    {
        OwnerId = ownerId;
        PlateNumber = plateNumber;
        Brand = brand;
        QrCodeHash = qrCodeHash;
        VehicleType = vehicleType;
        Color = color;
        MotorPictureUrl = motorPictureUrl;
        OrcrDocumentUrl = orcrDocumentUrl;
        IsPrimary = false;
    }

    public void SetPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
    }

    public void MarkAsPrimary()
    {
        IsPrimary = true;
    }

    public void Update(string plateNumber, string brand, VehicleType vehicleType, string? color = null, string? motorPictureUrl = null, string? orcrDocumentUrl = null)
    {
        PlateNumber = plateNumber;
        Brand = brand;
        VehicleType = vehicleType;
        if (!string.IsNullOrWhiteSpace(color)) Color = color;
        if (!string.IsNullOrWhiteSpace(motorPictureUrl)) MotorPictureUrl = motorPictureUrl;
        if (!string.IsNullOrWhiteSpace(orcrDocumentUrl)) OrcrDocumentUrl = orcrDocumentUrl;
    }

    public void Update(string plateNumber, string brand, VehicleType vehicleType, string qrCodeHash, string? color = null, string? motorPictureUrl = null, string? orcrDocumentUrl = null)
    {
        PlateNumber = plateNumber;
        Brand = brand;
        VehicleType = vehicleType;
        QrCodeHash = qrCodeHash;
        if (!string.IsNullOrWhiteSpace(color)) Color = color;
        if (!string.IsNullOrWhiteSpace(motorPictureUrl)) MotorPictureUrl = motorPictureUrl;
        if (!string.IsNullOrWhiteSpace(orcrDocumentUrl)) OrcrDocumentUrl = orcrDocumentUrl;
    }
}