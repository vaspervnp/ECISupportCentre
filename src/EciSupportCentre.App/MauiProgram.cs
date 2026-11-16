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
        services.AddSingleton<DataService>();

        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<InitiativesViewModel>();
        services.AddTransient<InitiativeDetailViewModel>();

        services.AddTransient<HomePage>();
        services.AddTransient<InitiativesPage>();
        services.AddTransient<InitiativeDetailPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
