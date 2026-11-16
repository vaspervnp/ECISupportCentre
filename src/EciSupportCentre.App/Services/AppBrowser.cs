namespace EciSupportCentre.App.Services;

/// <summary>Opens web pages in a Custom Tab (the system browser inside the app), in the EU blue.</summary>
internal static class AppBrowser
{
    private static readonly BrowserLaunchOptions Options = new()
    {
        LaunchMode = BrowserLaunchMode.SystemPreferred,
        TitleMode = BrowserTitleMode.Show,
        PreferredToolbarColor = Color.FromArgb("#003399"),
        PreferredControlColor = Colors.White,
    };

    /// <summary>Opens <paramref name="uri"/> if it is an https address; anything else is ignored.</summary>
    public static async Task OpenAsync(Uri? uri)
    {
        if (uri is { IsAbsoluteUri: true } && uri.Scheme == Uri.UriSchemeHttps)
        {
            await Browser.Default.OpenAsync(uri, Options);
        }
    }
}
