using System;

namespace ParkFlow.Domain.Entities;

public class UserNotification : BaseEntity
{
    public Guid UserAccountId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Subtitle { get; private set; }
    public string Body { get; private set; } = null!;
    public string Type { get; private set; } = "system";
    public string? ReferenceCode { get; private set; }
    public string? VehiclePlate { get; private set; }
    public string? ActionRoute { get; private set; }
    public string? ActionText { get; private set; }
    public string? Priority { get; private set; }
    public string? Issuer { get; private set; }
    public string? DriverName { get; private set; }
    public string? DriverRole { get; private set; }
    public string? VehicleBrand { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }

    public UserAccount? UserAccount { get; private set; }

    private UserNotification() { } // EF Core

    public UserNotification(
        Guid userAccountId,
        string title,
        string body,
        string type = "system",
        string? subtitle = null,
        string? referenceCode = null,
        string? vehiclePlate = null,
        string? actionRoute = null,
        string? actionText = null,
        string? priority = null,
        string? issuer = null,
        string? driverName = null,
        string? driverRole = null,
        string? vehicleBrand = null)
    {
        if (userAccountId == Guid.Empty)
            throw new ArgumentException("UserAccountId is required.", nameof(userAccountId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));

        UserAccountId = userAccountId;
        Title = title.Trim();
        Body = body.Trim();
        Type = string.IsNullOrWhiteSpace(type) ? "system" : type.Trim();
        Subtitle = subtitle?.Trim();
        ReferenceCode = referenceCode?.Trim();
        VehiclePlate = vehiclePlate?.Trim();
        ActionRoute = actionRoute?.Trim();
        ActionText = actionText?.Trim();
        Priority = priority?.Trim();
        Issuer = issuer?.Trim();
        DriverName = driverName?.Trim();
        DriverRole = driverRole?.Trim();
        VehicleBrand = vehicleBrand?.Trim();
        IsRead = false;
        ReadAt = null;
    }

    public void MarkAsRead()
    {
        if (!IsRead)
        {
            IsRead = true;
            ReadAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
