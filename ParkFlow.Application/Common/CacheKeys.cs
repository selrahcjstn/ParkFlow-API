using System;

namespace ParkFlow.Application.Common;

public static class CacheKeys
{
    public const string DashboardPrefix = "dashboard:";
    public const string SessionPrefix = "sessions:";
    public const string AnnouncementPrefix = "announcements:";
    public const string UserPrefix = "user:";
    public const string VehiclePrefix = "vehicles:";
    public const string SchedulePrefix = "schedules:";

    public static string DashboardSummary(int capacity) => $"{DashboardPrefix}summary:cap_{capacity}";

    public static string ActiveSessionCount(int capacity) => $"{SessionPrefix}count:cap_{capacity}";

    public static string ActiveSessions(int capacity) => $"{SessionPrefix}active:cap_{capacity}";

    public static string ActiveAnnouncement => $"{AnnouncementPrefix}active";

    public static string UserProfile(Guid userId) => $"{UserPrefix}profile:{userId}";

    public static string UserVehicles(Guid ownerId) => $"{VehiclePrefix}owner:{ownerId}";

    public static string UserSchedules(Guid userId) => $"{SchedulePrefix}user:{userId}";
}
