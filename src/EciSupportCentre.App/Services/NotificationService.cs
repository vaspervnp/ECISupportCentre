using EciSupportCentre.App.Localization;
using EciSupportCentre.Core.Notifications;
using EciSupportCentre.Core.Sync;
using EciSupportCentre.Data;

namespace EciSupportCentre.App.Services;

/// <summary>Shows notifications on the device (Android: <c>AndroidNotificationPoster</c>).</summary>
public interface INotificationPoster
{
    /// <summary>Creates or renames the notification channels (names follow the app language).</summary>
    void EnsureChannels();

    /// <summary>Shows or replaces the notification with the same <see cref="PlannedNotification.Key"/>.</summary>
    void Post(PlannedNotification notification, string title, string text);
}

/// <summary>After each sync: decides what to tell the user, shows it, and records it as handled.</summary>
public sealed class NotificationService(EciDatabase database, INotificationPoster poster)
{
    public async Task NotifyAsync(SyncReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var language = Loc.Instance.Language;
        var input = new NotificationInput
        {
            PendingChanges = await database.GetPendingChangesAsync(cancellationToken).ConfigureAwait(false),
            Initiatives = await database.GetInitiativesAsync(language, cancellationToken).ConfigureAwait(false),
            RemindersSent = await database.GetRemindersSentAsync(cancellationToken).ConfigureAwait(false),

            // The first sync fills the news table; those articles are not news to anyone.
            NewArticles = report.FirstSync ? [] : report.NewArticles ?? [],
            Preferences = AppSettings.NotificationPreferences,
            Today = DateOnly.FromDateTime(DateTime.Now),
        };

        var plan = NotificationPlanner.Plan(input);
        if (plan.Notifications.Count > 0)
        {
            poster.EnsureChannels();
            foreach (var notification in plan.Notifications)
            {
                var (title, text) = Describe(notification);
                poster.Post(notification, title, text);
            }
        }

        // Handled even when the user blocked notifications: otherwise a later "allow" would bring a flood of old events.
        await database.CompleteNotificationsAsync(plan.HandledChangeIds, plan.RemindersSent, DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Title and text in the app language.</summary>
    public static (string Title, string Text) Describe(PlannedNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var subject = notification.Subject ?? "";
        var title = notification.Kind switch
        {
            NotificationKind.NewInitiative => Loc.Get("Notif_NewInitiative"),
            NotificationKind.CollectionStarted => Loc.Get("Notif_CollectionStarted"),
            NotificationKind.CollectionEnding => notification.Number switch
            {
                <= 0 => Loc.Get("Notif_EndingToday"),
                1 => Loc.Get("Notif_EndingTomorrow"),
                var days => Loc.Format("Notif_EndingDays", days),
            },
            NotificationKind.CollectionEnded => Loc.Get("Notif_CollectionEnded"),
            NotificationKind.SignatureMilestone => Loc.Format("Notif_Milestone", notification.Number),
            NotificationKind.Successful => Loc.Get("Notif_Successful"),
            NotificationKind.Answered => Loc.Get("Notif_Answered"),
            NotificationKind.Failed => Loc.Get("Notif_Failed"),
            NotificationKind.Withdrawn => Loc.Get("Notif_Withdrawn"),
            NotificationKind.News => notification.Number == 1 ? Loc.Get("Notif_NewsOne") : Loc.Format("Notif_NewsMany", notification.Number),
            _ => Loc.Get("AppName"),
        };

        return (title, subject);
    }

    /// <summary>Name the user sees in the system notification settings.</summary>
    public static string ChannelName(Core.Notifications.NotificationChannel channel) => channel switch
    {
        Core.Notifications.NotificationChannel.NewInitiatives => Loc.Get("Notif_Channel_New"),
        Core.Notifications.NotificationChannel.CollectionEnding => Loc.Get("Notif_Channel_Ending"),
        Core.Notifications.NotificationChannel.Results => Loc.Get("Notif_Channel_Results"),
        _ => Loc.Get("Notif_Channel_News"),
    };
}

/// <summary>Opens the screen a tapped notification points to, as soon as the shell is ready.</summary>
public static class NotificationLaunch
{
    private static string? _pendingRoute;

    public static void OpenInitiative(long initiativeId) =>
        Request(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{ViewModels.AppRoutes.InitiativeRoute}?id={initiativeId}"));

    public static void OpenNews() => Request("//news");

    /// <summary>Called by the first page when it appears, for taps that started the app.</summary>
    public static void OpenPending() => MainThread.BeginInvokeOnMainThread(() => _ = OpenAsync());

    private static void Request(string route)
    {
        _pendingRoute = route;
        OpenPending();
    }

    private static async Task OpenAsync()
    {
        if (_pendingRoute is not { } route || Shell.Current?.CurrentPage is null)
        {
            return;
        }

        _pendingRoute = null;
        await Shell.Current.GoToAsync(route);
    }
}
