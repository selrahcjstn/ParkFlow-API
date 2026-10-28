namespace ParkFlow.Application.Common;

// No ownership transfer or identity merge is performed by vehicle registration.
public sealed class VehiclePlateConflictException(string message) : Exception(message);
