using EciSupportCentre.Core.Notifications;

namespace EciSupportCentre.App.Services;

/// <summary>User choices kept in the platform preferences. The settings screen arrives in M8.</summary>
public static class AppSettings
{
    private const string FollowAllKey = "notify_follow_all";
    private const string SyncIntervalKey = "sync_interval_hours";
    private const string WifiOnlyKey = "sync_wifi_only";

    /// <summary>Notify about every initiative (true) or only followed ones (false).</summary>
    public static bool FollowAll
    {
        get => Preferences.Default.Get(FollowAllKey, true);
        set => Preferences.Default.Set(FollowAllKey, value);
    }

    /// <summary>Hours between background syncs; WorkManager allows no less than 15 minutes.</summary>
    public static int SyncIntervalHours
    {
        get => Math.Clamp(Preferences.Default.Get(SyncIntervalKey, 12), 1, 24 * 7);
        set => Preferences.Default.Set(SyncIntervalKey, value);
    }

    /// <summary>Background syncs only on unmetered networks (Wi-Fi).</summary>
    public static bool WifiOnly
    {
        get => Preferences.Default.Get(WifiOnlyKey, false);
        set => Preferences.Default.Set(WifiOnlyKey, value);
    }

    public static NotificationPreferences NotificationPreferences => new() { FollowAll = FollowAll };
}
