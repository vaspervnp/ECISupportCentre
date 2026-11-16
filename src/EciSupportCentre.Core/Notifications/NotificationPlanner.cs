using System.Globalization;
using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Storage;

namespace EciSupportCentre.Core.Notifications;

/// <summary>Android notification channel an event belongs to; the user can turn each one off.</summary>
public enum NotificationChannel
{
    NewInitiatives,
    CollectionEnding,
    Results,
    News,
}

public enum NotificationKind
{
    /// <summary>A new initiative was registered.</summary>
    NewInitiative,

    /// <summary>Collection of signatures started.</summary>
    CollectionStarted,

    /// <summary>A few days (or one day, or hours) left to sign. <see cref="PlannedNotification.Number"/> holds the days left.</summary>
    CollectionEnding,

    /// <summary>Collection closed; signatures are being verified.</summary>
    CollectionEnded,

    /// <summary>Supporters crossed a milestone. <see cref="PlannedNotification.Number"/> holds the milestone.</summary>
    SignatureMilestone,

    /// <summary>Reached the thresholds and was submitted to the Commission.</summary>
    Successful,

    /// <summary>The Commission answered.</summary>
    Answered,

    /// <summary>Not enough support, before or after verification.</summary>
    Failed,

    Withdrawn,

    /// <summary>New articles in the news feed. <see cref="PlannedNotification.Number"/> holds how many.</summary>
    News,
}

/// <summary>One notification to show. The app turns <see cref="Kind"/> into text in the user's language.</summary>
public sealed record PlannedNotification
{
    /// <summary>
    /// Stable key, used as the Android notification tag: a later notification with the same key replaces the earlier one
    /// (e.g. "collection ended" is replaced by "not enough support").
    /// </summary>
    public required string Key { get; init; }

    public required NotificationKind Kind { get; init; }

    public required NotificationChannel Channel { get; init; }

    /// <summary>Initiative to open on tap; null for news.</summary>
    public long? InitiativeId { get; init; }

    /// <summary>Initiative or article title.</summary>
    public string? Subject { get; init; }

    /// <summary>Days left, milestone or article count, depending on <see cref="Kind"/>.</summary>
    public long Number { get; init; }
}

/// <summary>What the user wants to hear about.</summary>
public sealed record NotificationPreferences
{
    /// <summary>
    /// True: status, milestone and deadline notifications for every initiative.
    /// False: only for followed initiatives. New initiatives and news are not affected.
    /// </summary>
    public bool FollowAll { get; init; } = true;
}

/// <summary>Everything the planner needs, read from the database after a sync.</summary>
public sealed record NotificationInput
{
    public IReadOnlyList<InitiativeChange> PendingChanges { get; init; } = [];

    /// <summary>Initiatives in the app language (titles, follow state, end of collection).</summary>
    public IReadOnlyList<InitiativeListItem> Initiatives { get; init; } = [];

    /// <summary>Keys of deadline reminders already shown (see <see cref="NotificationPlan.RemindersSent"/>).</summary>
    public IReadOnlySet<string> RemindersSent { get; init; } = new HashSet<string>();

    /// <summary>New articles found by the last sync, newest first; empty when there are none.</summary>
    public IReadOnlyList<NewsItem> NewArticles { get; init; } = [];

    public NotificationPreferences Preferences { get; init; } = new();

    public required DateOnly Today { get; init; }
}

/// <summary>Result of <see cref="NotificationPlanner.Plan"/>.</summary>
public sealed record NotificationPlan
{
    public IReadOnlyList<PlannedNotification> Notifications { get; init; } = [];

    /// <summary>Changes handled (shown or deliberately skipped); mark them notified.</summary>
    public IReadOnlyList<long> HandledChangeIds { get; init; } = [];

    /// <summary>Deadline reminders to remember, so each is shown once.</summary>
    public IReadOnlyList<string> RemindersSent { get; init; } = [];
}

/// <summary>Decides which notifications a sync produces. Pure: no I/O, no clock.</summary>
public static class NotificationPlanner
{
    /// <summary>Days before the end of collection that get a reminder: a week before, and the last day(s).</summary>
    public static IReadOnlyList<int> ReminderDays { get; } = [7, 1];

    public static NotificationPlan Plan(NotificationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var byId = input.Initiatives.ToDictionary(i => i.Id);
        bool Wanted(long id) => input.Preferences.FollowAll || (byId.TryGetValue(id, out var i) && i.IsFollowed);
        string? Title(long id) => byId.TryGetValue(id, out var i) ? i.Title : null;

        var notifications = new List<PlannedNotification>();

        // 1. Recorded changes. Several syncs may have passed since the last notification:
        //    per initiative, only the latest status worth telling and the highest milestone count.
        foreach (var group in input.PendingChanges.GroupBy(c => c.InitiativeId))
        {
            var id = group.Key;
            var latestStatus = group
                .Where(c => c.Kind == ChangeKind.StatusChanged && ForStatus(c) is not null)
                .MaxBy(c => c.Id);
            var isNew = group.Any(c => c.Kind == ChangeKind.NewInitiative);

            if (isNew)
            {
                notifications.Add(new PlannedNotification
                {
                    Key = Key("status", id),
                    Kind = NotificationKind.NewInitiative,
                    Channel = NotificationChannel.NewInitiatives,
                    InitiativeId = id,
                    Subject = Title(id),
                });
            }
            else if (latestStatus is not null && Wanted(id) && ForStatus(latestStatus) is { } status)
            {
                notifications.Add(new PlannedNotification
                {
                    Key = Key("status", id),
                    Kind = status.Kind,
                    Channel = status.Channel,
                    InitiativeId = id,
                    Subject = Title(id),
                });
            }

            var milestone = group
                .Where(c => c.Kind == ChangeKind.SignatureMilestone)
                .Select(c => long.TryParse(c.NewValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? m : 0)
                .DefaultIfEmpty(0)
                .Max();

            // A success notification already says it all.
            var finished = latestStatus?.NewStatus.ToOutcome() is InitiativeOutcome.Successful or InitiativeOutcome.Failed;
            if (milestone > 0 && !isNew && !finished && Wanted(id))
            {
                notifications.Add(new PlannedNotification
                {
                    Key = Key("milestone", id),
                    Kind = NotificationKind.SignatureMilestone,
                    Channel = NotificationChannel.Results,
                    InitiativeId = id,
                    Subject = Title(id),
                    Number = milestone,
                });
            }

            // LastCall changes need no notification of their own: the deadline reminders below cover them.
        }

        // 2. Deadline reminders, from the end of collection. Each one at most once; when the first reminder
        //    is due late (e.g. the app was off for a week), only the most urgent one is shown.
        var reminders = new List<string>();
        foreach (var item in input.Initiatives)
        {
            if (item.DaysLeft(input.Today) is not { } daysLeft || !Wanted(item.Id))
            {
                continue;
            }

            var due = ReminderDays.Where(d => daysLeft <= d).ToList();
            var unsent = due.Where(d => !input.RemindersSent.Contains(ReminderKey(item.Id, d))).ToList();
            if (unsent.Count == 0)
            {
                continue;
            }

            notifications.Add(new PlannedNotification
            {
                Key = Key("ending", item.Id),
                Kind = NotificationKind.CollectionEnding,
                Channel = NotificationChannel.CollectionEnding,
                InitiativeId = item.Id,
                Subject = item.Title,
                Number = daysLeft,
            });

            // Remember every due reminder, so a late 7-day reminder is not followed by a second one on the same day.
            reminders.AddRange(due.Select(d => ReminderKey(item.Id, d)).Where(k => !input.RemindersSent.Contains(k)));
        }

        // 3. News: one notification for all new articles.
        if (input.NewArticles.Count > 0)
        {
            notifications.Add(new PlannedNotification
            {
                Key = "news",
                Kind = NotificationKind.News,
                Channel = NotificationChannel.News,
                Subject = input.NewArticles[0].Title,
                Number = input.NewArticles.Count,
            });
        }

        return new NotificationPlan
        {
            Notifications = notifications,
            HandledChangeIds = [.. input.PendingChanges.Select(c => c.Id)],
            RemindersSent = reminders,
        };
    }

    /// <summary>Key stored when a deadline reminder was shown.</summary>
    public static string ReminderKey(long initiativeId, int days) =>
        string.Create(CultureInfo.InvariantCulture, $"ending:{initiativeId}:{days}");

    private static string Key(string kind, long id) => string.Create(CultureInfo.InvariantCulture, $"{kind}:{id}");

    private static (NotificationKind Kind, NotificationChannel Channel)? ForStatus(InitiativeChange change)
    {
        var from = EciStatusExtensions.ParseStatus(change.OldValue);
        return change.NewStatus switch
        {
            EciStatus.Ongoing => (NotificationKind.CollectionStarted, NotificationChannel.NewInitiatives),

            // Closed and Verification follow each other; only leaving the collection is news.
            EciStatus.Closed or EciStatus.Verification when from == EciStatus.Ongoing =>
                (NotificationKind.CollectionEnded, NotificationChannel.CollectionEnding),
            EciStatus.Closed or EciStatus.Verification => null,

            EciStatus.Submitted => (NotificationKind.Successful, NotificationChannel.Results),
            EciStatus.Answered => (NotificationKind.Answered, NotificationChannel.Results),
            EciStatus.InsufficientSupport or EciStatus.InsufficientSupportAfterVerification =>
                (NotificationKind.Failed, NotificationChannel.Results),
            EciStatus.Withdrawn => (NotificationKind.Withdrawn, NotificationChannel.Results),
            _ => null,
        };
    }
}
