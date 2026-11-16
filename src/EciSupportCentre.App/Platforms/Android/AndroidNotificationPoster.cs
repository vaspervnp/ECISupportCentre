using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using EciSupportCentre.App.Services;
using EciSupportCentre.Core.Notifications;
using Channel = EciSupportCentre.Core.Notifications.NotificationChannel;

namespace EciSupportCentre.App;

/// <summary>Posts notifications through <see cref="NotificationManagerCompat"/>, one Android channel per <see cref="Channel"/>.</summary>
public sealed class AndroidNotificationPoster : INotificationPoster
{
    public const string ExtraInitiativeId = "eci_initiative_id";
    public const string ExtraOpenNews = "eci_open_news";

    private static readonly Channel[] Channels = Enum.GetValues<Channel>();

    private static Context Context => Android.App.Application.Context;

    public void EnsureChannels()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var manager = (NotificationManager?)Context.GetSystemService(Context.NotificationService);
        foreach (var channel in Channels)
        {
            var importance = channel == Channel.News ? NotificationImportance.Low : NotificationImportance.Default;

            // Creating an existing channel only updates its name, so this also follows a language change.
            manager?.CreateNotificationChannel(new Android.App.NotificationChannel(ChannelId(channel), NotificationService.ChannelName(channel), importance));
        }
    }

    public void Post(PlannedNotification notification, string title, string text)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var intent = new Intent(Context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        int requestCode;
        if (notification.InitiativeId is { } id)
        {
            intent.PutExtra(ExtraInitiativeId, id);
            requestCode = unchecked((int)id);
        }
        else
        {
            intent.PutExtra(ExtraOpenNews, true);
            requestCode = 0;
        }

        var tap = PendingIntent.GetActivity(Context, requestCode, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(Context, ChannelId(notification.Channel));
        builder.SetSmallIcon(Resource.Drawable.ic_notification);
        builder.SetColor(unchecked((int)0xFF003399));
        builder.SetContentTitle(title);
        builder.SetContentText(text);
        builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(text));
        builder.SetContentIntent(tap);
        builder.SetAutoCancel(true);
        builder.SetCategory(NotificationCompat.CategoryRecommendation);

        // Without permission (Android 13+) or with notifications turned off nothing is shown.
        var manager = NotificationManagerCompat.From(Context);
        if (manager?.AreNotificationsEnabled() == true && builder.Build() is { } built)
        {
            manager.Notify(notification.Key, 1, built);
        }
    }

    public void OpenSystemSettings()
    {
        Intent intent;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            intent = new Intent(Android.Provider.Settings.ActionAppNotificationSettings);
            intent.PutExtra(Android.Provider.Settings.ExtraAppPackage, Context.PackageName);
        }
        else
        {
            intent = new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings, Android.Net.Uri.Parse("package:" + Context.PackageName));
        }

        intent.AddFlags(ActivityFlags.NewTask);
        Context.StartActivity(intent);
    }

    private static string ChannelId(Channel channel) => channel switch
    {
        Channel.NewInitiatives => "new_initiatives",
        Channel.CollectionEnding => "collection_ending",
        Channel.Results => "results",
        _ => "news",
    };
}
