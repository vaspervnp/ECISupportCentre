using System.Globalization;
using EciSupportCentre.Core;

namespace EciSupportCentre.App;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnOpenWebsiteClicked(object? sender, EventArgs e)
    {
        var uri = EciSources.Website(CultureInfo.CurrentUICulture.Name);
        await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
    }
}
