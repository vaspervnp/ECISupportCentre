using CommunityToolkit.Mvvm.Input;
using EciSupportCentre.App.Localization;
using EciSupportCentre.App.Services;
using EciSupportCentre.Core;

namespace EciSupportCentre.App.ViewModels;

/// <summary>The About page: what the app is, where its data comes from, privacy and licence.</summary>
public sealed partial class AboutViewModel
{
    public static Uri SourceCode { get; } = new("https://github.com/vaspervnp/ECISupportCentre");

    public string VersionText { get; } = $"{AppInfo.Current.Name} {AppInfo.Current.VersionString}";

    [RelayCommand]
    private static Task OpenWebsiteAsync() => AppBrowser.OpenAsync(EciSources.Website(Loc.Instance.Language));

    [RelayCommand]
    private static Task OpenSourceCodeAsync() => AppBrowser.OpenAsync(SourceCode);
}
