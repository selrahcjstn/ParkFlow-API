using ParkFlow.Domain.Enums;
namespace ParkFlow.Application.Features.ParkingLogs.Services;

public static class DriverEntryPolicy
{
    public static string? MissingRequirement(UserProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ProfilePictureUrl))
            return "Upload a profile photo in Driver documents before vehicle entry.";
        if (string.IsNullOrWhiteSpace(profile.DriversLicenseUrl))
            return "Upload your driver's license in Driver documents before vehicle entry.";
        if (profile.LicenseStatus != CorVerificationStatus.Verified)
            return profile.LicenseStatus == CorVerificationStatus.Rejected
                ? "Driver's license rejected: " + profile.LicenseRejectionReason
                : "Your driver's license is waiting for admin review in Vehicles.";
        return null;
    }
}
