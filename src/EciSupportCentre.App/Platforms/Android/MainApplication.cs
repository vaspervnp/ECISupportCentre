using Android.App;
using Android.Runtime;
using EciSupportCentre.App.Services;

namespace EciSupportCentre.App;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();

        // Runs for the app and for background jobs alike.
        IPlatformApplication.Current?.Services.GetService<INotificationPoster>()?.EnsureChannels();
        BackgroundSync.Schedule(this);
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
