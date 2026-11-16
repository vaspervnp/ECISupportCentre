using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EciSupportCentre.App.Localization;
using EciSupportCentre.App.Services;
using EciSupportCentre.Core.Localization;

namespace EciSupportCentre.App.ViewModels;

/// <summary>One entry of the language picker; an empty code follows the device.</summary>
public sealed record LanguageOption(string Code, string Name);

/// <summary>The Settings tab: language, notifications, about.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly DataService _data;
    private readonly INotificationPoster _poster;
    private bool _building;

    public SettingsViewModel(DataService data, INotificationPoster poster)
    {
        _data = data;
        _poster = poster;
        BuildLanguages();
        Loc.Instance.LanguageChanged += (_, _) => MainThread.BeginInvokeOnMainThread(BuildLanguages);
    }

    [ObservableProperty]
    public partial IReadOnlyList<LanguageOption> Languages { get; set; } = [];

    [ObservableProperty]
    public partial LanguageOption? SelectedLanguage { get; set; }

    /// <summary>Notify about every initiative; off means only followed ones.</summary>
    [ObservableProperty]
    public partial bool NotifyAll { get; set; } = AppSettings.FollowAll;

    public string VersionText { get; } = $"{AppInfo.Current.Name} {AppInfo.Current.VersionString}";

    partial void OnNotifyAllChanged(bool value) => AppSettings.FollowAll = value;

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

    /// <summary>The names stay in their own language; only "Device language (…)" follows the app language.</summary>
    private void BuildLanguages()
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
        }
        finally
        {
            _building = false;
        }
    }
}
