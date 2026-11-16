using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using EciSupportCentre.App.Services;

namespace EciSupportCentre.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // A restored activity keeps its old intent; only a fresh start comes from a tap.
        if (savedInstanceState is null)
        {
            HandleNotificationTap(Intent);
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleNotificationTap(intent);
    }

    private static void HandleNotificationTap(Intent? intent)
    {
        if (intent is null)
        {
            return;
        }

        var initiativeId = intent.GetLongExtra(AndroidNotificationPoster.ExtraInitiativeId, 0);
        if (initiativeId > 0)
        {
            NotificationLaunch.OpenInitiative(initiativeId);
        }
        else if (intent.GetBooleanExtra(AndroidNotificationPoster.ExtraOpenNews, false))
        {
            NotificationLaunch.OpenNews();
        }

#if DEBUG
        if (intent.GetBooleanExtra("eci_debug_sync", false))
        {
            BackgroundSync.RunOnceSoon(Android.App.Application.Context);
            intent.RemoveExtra("eci_debug_sync");
        }
#endif

        intent.RemoveExtra(AndroidNotificationPoster.ExtraInitiativeId);
        intent.RemoveExtra(AndroidNotificationPoster.ExtraOpenNews);
    }
}
