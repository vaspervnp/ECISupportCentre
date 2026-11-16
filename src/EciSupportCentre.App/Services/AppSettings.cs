using EciSupportCentre.Core.Notifications;

namespace EciSupportCentre.App.Services;

/// <summary>User choices kept in the platform preferences, changed on the Settings tab.</summary>
public static class AppSettings
{
    private const string FollowAllKey = "notify_follow_all";
    private const string SyncIntervalKey = "sync_interval_hours";
    private const string WifiOnlyKey = "sync_wifi_only";
    private const string LanguageKey = "app_language";
    private const string ThemeKey = "app_theme";

    /// <summary>Choices offered for <see cref="SyncIntervalHours"/>.</summary>
    public static IReadOnlyList<int> SyncIntervals { get; } = [3, 6, 12, 24];

    /// <summary>EU language code chosen by the user; empty to follow the device language.</summary>
    public static string Language
    {
        get => Preferences.Default.Get(LanguageKey, "");
        set => Preferences.Default.Set(LanguageKey, value);
    }

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

    /// <summary>Automatic syncs (background and on opening the app) only on unmetered networks; pull to refresh always works.</summary>
    public static bool WifiOnly
    {
        get => Preferences.Default.Get(WifiOnlyKey, false);
        set => Preferences.Default.Set(WifiOnlyKey, value);
    }

    /// <summary>Light, dark, or <see cref="AppTheme.Unspecified"/> to follow the system.</summary>
    public static AppTheme Theme
    {
        get => Preferences.Default.Get(ThemeKey, (int)AppTheme.Unspecified) switch
        {
            (int)AppTheme.Light => AppTheme.Light,
            (int)AppTheme.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };
        set => Preferences.Default.Set(ThemeKey, (int)value);
    }

    public static NotificationPreferences NotificationPreferences => new() { FollowAll = FollowAll };
}
