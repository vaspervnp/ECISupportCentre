using EciSupportCentre.App.Localization;
using EciSupportCentre.App.Services;
using EciSupportCentre.Core;

namespace EciSupportCentre.App.Views;

/// <summary>Which official page a <see cref="SourceLink"/> points to.</summary>
public enum SourcePage
{
    Initiatives,
    News,
    Website,
}

/// <summary>
/// Shows and opens the official page that the data on screen comes from, with its address written out
/// (Google Play asks apps with government information to link their source clearly).
/// </summary>
public partial class SourceLink : ContentView
{
    public static readonly BindableProperty PageProperty = BindableProperty.Create(
        nameof(Page), typeof(SourcePage), typeof(SourceLink), SourcePage.Website,
        propertyChanged: (view, _, _) => ((SourceLink)view).Refresh());

    public SourceLink()
    {
        InitializeComponent();
        Loc.Instance.LanguageChanged += (_, _) => Refresh();
        Refresh();
    }

    public SourcePage Page
    {
        get => (SourcePage)GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    /// <summary>The page in the current app language.</summary>
    public Uri Url => Address(Page, Loc.Instance.Language);

    /// <summary>The official page for <paramref name="page"/> in <paramref name="language"/>.</summary>
    public static Uri Address(SourcePage page, string language) => page switch
    {
        SourcePage.Initiatives => EciSources.InitiativesPage(language),
        SourcePage.News => EciSources.NewsPage(language),
        _ => EciSources.Website(language),
    };

    private void Refresh()
    {
        // Host and path without the language suffix, e.g. "citizens-initiative.europa.eu/find-initiative".
        var path = System.Text.RegularExpressions.Regex.Replace(Url.AbsolutePath, "_[a-z]{2}$", "");
        var shown = Url.Host + (path == "/index" ? "" : path);
        Text.Text = Loc.Format("Source_Link", shown);
        SemanticProperties.SetHint(Text, Loc.Get("About_Website"));
    }

    private async void OnTapped(object? sender, TappedEventArgs e) => await AppBrowser.OpenAsync(Url);
}
