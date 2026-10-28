namespace ParkFlow.Domain.Entities;

// Derived database authority maintained by source-table triggers, never an identity transfer.
public sealed class VehiclePlateOwnership
{
    public string NormalizedPlateNumber { get; private set; } = null!;
    public Guid? VehicleId { get; private set; }
    public Guid? VisitorVehicleId { get; private set; }
}
