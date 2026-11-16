using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EciSupportCentre.App.Localization;
using EciSupportCentre.App.Services;
using EciSupportCentre.Core.Localization;

namespace EciSupportCentre.App.ViewModels;

/// <summary>One entry of the language picker; an empty code follows the device.</summary>
public sealed record LanguageOption(string Code, string Name);

/// <summary>One entry of a picker with a numeric value (theme, sync interval).</summary>
public sealed record Choice(int Value, string Name);

/// <summary>The Settings tab: language, appearance, updates, notifications, about.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly DataService _data;
    private readonly INotificationPoster _poster;
    private readonly ISyncScheduler _scheduler;
    private bool _building;

    public SettingsViewModel(DataService data, INotificationPoster poster, ISyncScheduler scheduler)
    {
        _data = data;
        _poster = poster;
        _scheduler = scheduler;
        BuildOptions();
        Loc.Instance.LanguageChanged += (_, _) => MainThread.BeginInvokeOnMainThread(BuildOptions);
    }

    [ObservableProperty]
    public partial IReadOnlyList<LanguageOption> Languages { get; set; } = [];

    [ObservableProperty]
    public partial LanguageOption? SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Choice> Themes { get; set; } = [];

    [ObservableProperty]
    public partial Choice? SelectedTheme { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Choice> SyncIntervals { get; set; } = [];

    [ObservableProperty]
    public partial Choice? SelectedSyncInterval { get; set; }

    /// <summary>Automatic updates only on Wi-Fi.</summary>
    [ObservableProperty]
    public partial bool WifiOnly { get; set; } = AppSettings.WifiOnly;

    /// <summary>Notify about every initiative; off means only followed ones.</summary>
    [ObservableProperty]
    public partial bool NotifyAll { get; set; } = AppSettings.FollowAll;

    public string VersionText { get; } = $"{AppInfo.Current.Name} {AppInfo.Current.VersionString}";

    partial void OnNotifyAllChanged(bool value) => AppSettings.FollowAll = value;

    partial void OnWifiOnlyChanged(bool value)
    {
        AppSettings.WifiOnly = value;
        _scheduler.Reschedule();
    }

    partial void OnSelectedThemeChanged(Choice? value)
    {
        if (_building || value is null)
        {
            return;
        }

        AppSettings.Theme = (AppTheme)value.Value;
        if (Application.Current is { } app)
        {
            app.UserAppTheme = AppSettings.Theme;
        }
    }

    partial void OnSelectedSyncIntervalChanged(Choice? value)
    {
        if (_building || value is null || value.Value == AppSettings.SyncIntervalHours)
        {
            return;
        }

        AppSettings.SyncIntervalHours = value.Value;
        _scheduler.Reschedule();
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_building || value is null || value.Code == AppSettings.Language)
        {
            return;
        }

        AppSettings.Language = value.Code;

        // Every page listens to this and redraws in the new language, without a restart.
        Loc.Instance.Apply(value.Code);
        _poster.EnsureChannels();

        // Titles, signing links and news of the new language come from the register; until then English is shown.
        _ = _data.SyncAsync();
    }

    [RelayCommand]
    private void OpenNotificationSettings() => _poster.OpenSystemSettings();

    [RelayCommand]
    private static Task OpenAboutAsync() => Shell.Current.GoToAsync(AppRoutes.AboutRoute);

    /// <summary>
    /// Fills the pickers in the app language. Language names stay in their own language; only
    /// "Device language (…)" follows the app language.
    /// </summary>
    private void BuildOptions()
    {
        _building = true;
        try
        {
            var device = LanguageChoice.Find(Loc.DeviceCulture.TwoLetterISOLanguageName) ?? LanguageChoice.Find("en")!;
            Languages =
            [
                new LanguageOption("", Loc.Format("Settings_DeviceLanguage", device.NativeName)),
                .. LanguageChoice.All.Select(l => new LanguageOption(l.Code, l.NativeName)),
            ];
            SelectedLanguage = Languages.FirstOrDefault(l => l.Code == AppSettings.Language) ?? Languages[0];

            Themes =
            [
                new Choice((int)AppTheme.Unspecified, Loc.Get("Theme_System")),
                new Choice((int)AppTheme.Light, Loc.Get("Theme_Light")),
                new Choice((int)AppTheme.Dark, Loc.Get("Theme_Dark")),
            ];
            SelectedTheme = Themes.FirstOrDefault(c => c.Value == (int)AppSettings.Theme) ?? Themes[0];

            SyncIntervals = [.. AppSettings.SyncIntervals.Select(h => new Choice(h, Loc.Get($"Sync_Every{h}h")))];
            SelectedSyncInterval = SyncIntervals.FirstOrDefault(c => c.Value == AppSettings.SyncIntervalHours)
                ?? SyncIntervals.First(c => c.Value == 12);
        }
        finally
        {
            _building = false;
        }
    }
}
