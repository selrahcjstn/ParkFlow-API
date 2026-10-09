namespace ParkFlow.Application.Common;

// Persistence reports an ID constraint conflict without exposing database details.
public sealed class RegistrationIdConflictException(string message, Exception innerException)
    : Exception(message, innerException);
