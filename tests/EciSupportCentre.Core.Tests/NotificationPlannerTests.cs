using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Notifications;
using EciSupportCentre.Core.Storage;

namespace EciSupportCentre.Core.Tests;

public class NotificationPlannerTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static long _nextChangeId = 1;

    private static InitiativeListItem Item(long id, string status = "ONGOING", int? daysLeft = null, bool followed = false) => new()
    {
        Id = id,
        PubRegNum = $"ECI(2026){id:000000}",
        Year = "2026",
        Number = $"{id:000000}",
        Status = EciStatusExtensions.ParseStatus(status),
        Title = $"Initiative {id}",
        TitleLanguage = "en",
        CollectionEndDate = daysLeft is { } d ? Today.AddDays(d) : null,
        IsFollowed = followed,
    };

    private static InitiativeChange Change(long initiativeId, ChangeKind kind, string? oldValue = null, string? newValue = null) => new()
    {
        Id = _nextChangeId++,
        InitiativeId = initiativeId,
        Kind = kind,
        OldValue = oldValue,
        NewValue = newValue,
        DetectedAt = DateTimeOffset.UnixEpoch,
    };

    private static NotificationPlan Plan(
        IReadOnlyList<InitiativeChange>? changes = null,
        IReadOnlyList<InitiativeListItem>? items = null,
        bool followAll = true,
        IReadOnlySet<string>? sent = null,
        IReadOnlyList<NewsItem>? news = null) =>
        NotificationPlanner.Plan(new NotificationInput
        {
            PendingChanges = changes ?? [],
            Initiatives = items ?? [],
            Preferences = new NotificationPreferences { FollowAll = followAll },
            RemindersSent = sent ?? new HashSet<string>(),
            NewArticles = news ?? [],
            Today = Today,
        });

    [Theory]
    [InlineData("REGISTERED", "ONGOING", NotificationKind.CollectionStarted, NotificationChannel.NewInitiatives)]
    [InlineData("ONGOING", "CLOSED", NotificationKind.CollectionEnded, NotificationChannel.CollectionEnding)]
    [InlineData("ONGOING", "VERIFICATION", NotificationKind.CollectionEnded, NotificationChannel.CollectionEnding)]
    [InlineData("VERIFICATION", "SUBMITTED", NotificationKind.Successful, NotificationChannel.Results)]
    [InlineData("SUBMITTED", "ANSWERED", NotificationKind.Answered, NotificationChannel.Results)]
    [InlineData("ONGOING", "INSUFFICIENT_SUPPORT", NotificationKind.Failed, NotificationChannel.Results)]
    [InlineData("VERIFICATION", "INSUFFICIENT_SUPPORT_AFTER_VERIFICATION", NotificationKind.Failed, NotificationChannel.Results)]
    [InlineData("ONGOING", "WITHDRAWN", NotificationKind.Withdrawn, NotificationChannel.Results)]
    public void Status_changes_become_notifications(string from, string to, NotificationKind kind, NotificationChannel channel)
    {
        var plan = Plan([Change(5, ChangeKind.StatusChanged, from, to)], [Item(5, to)]);

        var n = Assert.Single(plan.Notifications);
        Assert.Equal((kind, channel, 5L, "Initiative 5", "status:5"), (n.Kind, n.Channel, n.InitiativeId, n.Subject, n.Key));
    }

    [Theory]
    [InlineData("CLOSED", "VERIFICATION")]
    [InlineData("ONGOING", "UNKNOWN_NEW_CODE")]
    public void Steps_that_are_not_news_are_handled_silently(string from, string to)
    {
        var change = Change(5, ChangeKind.StatusChanged, from, to);
        var plan = Plan([change], [Item(5, "VERIFICATION")]);

        Assert.Empty(plan.Notifications);
        Assert.Equal([change.Id], plan.HandledChangeIds);
    }

    [Fact]
    public void New_initiatives_are_announced_even_when_only_followed_ones_are_wanted()
    {
        var plan = Plan([Change(9, ChangeKind.NewInitiative, null, "REGISTERED")], [Item(9, "REGISTERED")], followAll: false);

        var n = Assert.Single(plan.Notifications);
        Assert.Equal((NotificationKind.NewInitiative, NotificationChannel.NewInitiatives), (n.Kind, n.Channel));
    }

    [Fact]
    public void Without_follow_all_only_followed_initiatives_notify_but_every_change_is_handled()
    {
        var changes = new[]
        {
            Change(1, ChangeKind.StatusChanged, "ONGOING", "INSUFFICIENT_SUPPORT"),
            Change(2, ChangeKind.StatusChanged, "ONGOING", "INSUFFICIENT_SUPPORT"),
            Change(2, ChangeKind.SignatureMilestone, "1", "100000"),
        };

        var plan = Plan(changes, [Item(1, "INSUFFICIENT_SUPPORT", followed: true), Item(2, "INSUFFICIENT_SUPPORT")], followAll: false);

        Assert.Equal([1L], plan.Notifications.Select(n => n.InitiativeId!.Value));
        Assert.Equal(changes.Select(c => c.Id), plan.HandledChangeIds);
    }

    [Fact]
    public void Missed_syncs_collapse_to_the_latest_status_and_highest_milestone()
    {
        var plan = Plan(
            [
                Change(3, ChangeKind.SignatureMilestone, "90000", "100000"),
                Change(3, ChangeKind.StatusChanged, "ONGOING", "CLOSED"),
                Change(3, ChangeKind.SignatureMilestone, "100000", "250000"),
                Change(3, ChangeKind.StatusChanged, "CLOSED", "VERIFICATION"),
                Change(3, ChangeKind.LastCall),
            ],
            [Item(3, "VERIFICATION")]);

        // CLOSED → VERIFICATION is silent, so the earlier "collection ended" is the one to tell.
        Assert.Equal(
            [(NotificationKind.CollectionEnded, 0L), (NotificationKind.SignatureMilestone, 250_000L)],
            plan.Notifications.Select(n => (n.Kind, n.Number)));
        Assert.Equal(5, plan.HandledChangeIds.Count);
    }

    [Fact]
    public void Only_the_latest_of_several_results_is_told()
    {
        var plan = Plan(
            [
                Change(6, ChangeKind.StatusChanged, "ONGOING", "CLOSED"),
                Change(6, ChangeKind.StatusChanged, "VERIFICATION", "SUBMITTED"),
                Change(6, ChangeKind.StatusChanged, "SUBMITTED", "ANSWERED"),
            ],
            [Item(6, "ANSWERED")]);

        Assert.Equal([NotificationKind.Answered], plan.Notifications.Select(n => n.Kind));
    }

    [Fact]
    public void A_result_replaces_the_milestone_reached_on_the_way()
    {
        var plan = Plan(
            [
                Change(4, ChangeKind.SignatureMilestone, "900000", "1000000"),
                Change(4, ChangeKind.StatusChanged, "VERIFICATION", "SUBMITTED"),
            ],
            [Item(4, "SUBMITTED")]);

        Assert.Equal([NotificationKind.Successful], plan.Notifications.Select(n => n.Kind));
    }

    [Theory]
    [InlineData(10, "", null, "")]
    [InlineData(8, "", null, "")]
    [InlineData(7, "", 7, "7")]
    [InlineData(3, "", 3, "7")]
    [InlineData(3, "7", null, "")]
    [InlineData(1, "7", 1, "1")]
    [InlineData(0, "7", 0, "1")]
    [InlineData(1, "", 1, "7,1")]
    [InlineData(0, "7,1", null, "")]
    public void Deadline_reminders_a_week_before_and_on_the_last_day_each_once(int daysLeft, string alreadySent, int? shownDaysLeft, string recorded)
    {
        var sent = alreadySent.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => NotificationPlanner.ReminderKey(8, int.Parse(d, System.Globalization.CultureInfo.InvariantCulture)))
            .ToHashSet();

        var plan = Plan(items: [Item(8, daysLeft: daysLeft)], sent: sent);

        Assert.Equal(shownDaysLeft, plan.Notifications.SingleOrDefault()?.Number is { } n ? (int)n : null);
        var expected = recorded.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => NotificationPlanner.ReminderKey(8, int.Parse(d, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(expected, plan.RemindersSent);
        if (shownDaysLeft is not null)
        {
            Assert.Equal((NotificationChannel.CollectionEnding, "ending:8"), (plan.Notifications[0].Channel, plan.Notifications[0].Key));
        }
    }

    [Fact]
    public void Deadline_reminders_only_while_collecting_and_for_wanted_initiatives()
    {
        var items = new[]
        {
            Item(1, "CLOSED", daysLeft: 2),
            Item(2, "ONGOING", daysLeft: 2),
            Item(3, "ONGOING", daysLeft: 2, followed: true),
            Item(4, "ONGOING"), // end of collection not known yet
        };

        Assert.Equal([2L, 3L], Plan(items: items).Notifications.Select(n => n.InitiativeId!.Value));
        Assert.Equal([3L], Plan(items: items, followAll: false).Notifications.Select(n => n.InitiativeId!.Value));
    }

    [Fact]
    public void New_articles_make_one_notification()
    {
        NewsItem Article(string title) => new() { Id = title, Title = title, Language = "en" };

        var n = Assert.Single(Plan(news: [Article("Newest"), Article("Older")]).Notifications);

        Assert.Equal((NotificationKind.News, NotificationChannel.News, "news", "Newest", 2L, (long?)null), (n.Kind, n.Channel, n.Key, n.Subject, n.Number, n.InitiativeId));
    }
}
