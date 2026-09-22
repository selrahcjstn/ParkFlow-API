using System;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Notifications.DTOs;

public class UserNotificationDto
{
    public Guid Id { get; set; }
    public Guid UserAccountId { get; set; }
    public string Title { get; set; } = null!;
    public string? Subtitle { get; set; }
    public string Body { get; set; } = null!;
    public string Type { get; set; } = "system";
    public string? ReferenceCode { get; set; }
    public string? VehiclePlate { get; set; }
    public string? ActionRoute { get; set; }
    public string? ActionText { get; set; }
    public string? Priority { get; set; }
    public string? Issuer { get; set; }
    public string? DriverName { get; set; }
    public string? DriverRole { get; set; }
    public string? VehicleBrand { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public static UserNotificationDto FromEntity(UserNotification entity)
    {
        return new UserNotificationDto
        {
            Id = entity.Id,
            UserAccountId = entity.UserAccountId,
            Title = entity.Title,
            Subtitle = entity.Subtitle,
            Body = entity.Body,
            Type = entity.Type,
            ReferenceCode = entity.ReferenceCode,
            VehiclePlate = entity.VehiclePlate,
            ActionRoute = entity.ActionRoute,
            ActionText = entity.ActionText,
            Priority = entity.Priority,
            Issuer = entity.Issuer,
            DriverName = entity.DriverName,
            DriverRole = entity.DriverRole,
            VehicleBrand = entity.VehicleBrand,
            IsRead = entity.IsRead,
            ReadAt = entity.ReadAt,
            CreatedAt = entity.CreatedAt,
        };
    }
}
