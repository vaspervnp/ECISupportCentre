using EciSupportCentre.App.Services;
using EciSupportCentre.App.ViewModels;
using EciSupportCentre.App.Views;
using EciSupportCentre.Core.Api;
using EciSupportCentre.Core.Storage;
using EciSupportCentre.Core.Sync;
using EciSupportCentre.Data;
using Microsoft.Extensions.Logging;

namespace EciSupportCentre.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Before anything reads a text: the app and background jobs alike.
        Localization.Loc.Instance.Apply(AppSettings.Language);

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var services = builder.Services;
        services.AddSingleton(_ => EciHttp.CreateClient(appVersion: AppInfo.Current.VersionString));
        services.AddSingleton<IRegisterApiClient>(sp => new RegisterApiClient(sp.GetRequiredService<HttpClient>()));
        services.AddSingleton<INewsFeedClient>(sp => new NewsFeedClient(sp.GetRequiredService<HttpClient>()));
        services.AddSingleton(_ => new EciDatabase(Path.Combine(FileSystem.AppDataDirectory, "eci.db")));
        services.AddSingleton<IEciStore>(sp => sp.GetRequiredService<EciDatabase>());
        services.AddSingleton(sp => new SyncEngine(
            sp.GetRequiredService<IRegisterApiClient>(),
            sp.GetRequiredService<INewsFeedClient>(),
            sp.GetRequiredService<IEciStore>()));
        services.AddSingleton(Connectivity.Current);
        services.AddSingleton<INotificationPoster, AndroidNotificationPoster>();
        services.AddSingleton<ISyncScheduler, AndroidSyncScheduler>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<DataService>();

        services.AddSingleton<SyncStatusViewModel>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<InitiativesViewModel>();
        services.AddTransient<InitiativeDetailViewModel>();
        services.AddSingleton<NewsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AboutViewModel>();

        services.AddTransient<HomePage>();
        services.AddTransient<InitiativesPage>();
        services.AddTransient<InitiativeDetailPage>();
        services.AddTransient<NewsPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<AboutPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
