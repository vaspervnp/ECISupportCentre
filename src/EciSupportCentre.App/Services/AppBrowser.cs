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

    /// <summary>
    /// Opens a web address. Plain http is upgraded to https (the register links EUR-Lex documents with http);
    /// any other scheme is ignored.
    /// </summary>
    public static async Task OpenAsync(Uri? uri)
    {
        if (uri is not { IsAbsoluteUri: true })
        {
            return;
        }

        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            uri = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            await Browser.Default.OpenAsync(uri, Options);
        }
    }
}
