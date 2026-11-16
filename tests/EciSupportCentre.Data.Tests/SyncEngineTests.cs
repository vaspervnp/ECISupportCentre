using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Storage;
using EciSupportCentre.Core.Sync;

namespace EciSupportCentre.Data.Tests;

/// <summary>Sync scenarios against a real SQLite database and a fake register.</summary>
public class SyncEngineTests : DatabaseTest
{
    private readonly FakeRegister _register = new();
    private readonly FakeNews _news = new();

    private SyncEngine Engine(int maxDetails = 30) => new(_register, _news, Db, Time) { MaxDetailsPerSync = maxDetails };

    private void SeedRegister()
    {
        _register.Set(
            Make.Summary(1, "ONGOING", supporters: 90_000),
            Make.Summary(2, "ONGOING", supporters: 500),
            Make.Summary(3, "REGISTERED"),
            Make.Summary(4, "ANSWERED", supporters: 1_200_000),
            Make.Summary(5, "WITHDRAWN"));
        _news.Items.AddRange([FakeNews.Item(1), FakeNews.Item(2)]);
    }

    [Fact]
    public async Task First_sync_fills_the_database_without_recording_changes()
    {
        SeedRegister();

        var report = await Engine().SyncAsync("en");

        Assert.True(report.FirstSync);
        Assert.Equal(5, report.InitiativeCount);
        Assert.Empty(report.Changes);
        Assert.Empty(await Db.GetPendingChangesAsync());
        Assert.Equal(5, (await Db.GetInitiativesAsync("en")).Count);

        // Details only for initiatives that are still active.
        Assert.Equal(3, report.DetailsFetched);
        Assert.Equal([1, 2, 3], _register.DetailRequests.Order());
        Assert.Equal(0, report.DetailsPending);

        Assert.Equal(2, report.NewsAdded);
        Assert.Equal(2, (await Db.GetNewsAsync("en")).Count);
        Assert.Equal(Time.Now, await Db.GetLastSyncAsync());
        Assert.Equal(5, (await Db.GetStatisticsAsync())?.Registered);
        Assert.Equal("en", await Db.GetStateAsync(StateKeys.LastSyncLanguage));
    }

    [Fact]
    public async Task Second_sync_without_changes_records_nothing_and_fetches_no_details()
    {
        SeedRegister();
        await Engine().SyncAsync("en");
        _register.DetailRequests.Clear();

        var report = await Engine().SyncAsync("en");

        Assert.False(report.FirstSync);
        Assert.Empty(report.Changes);
        Assert.Equal(0, report.DetailsFetched);
        Assert.Empty(_register.DetailRequests);
        Assert.Equal(0, report.NewsAdded);
        Assert.Empty(await Db.GetPendingChangesAsync());
    }

    [Fact]
    public async Task Second_sync_records_only_real_changes()
    {
        SeedRegister();
        await Engine().SyncAsync("en");
        _register.DetailRequests.Clear();
        Time.Advance(TimeSpan.FromHours(12));

        var later = new DateTime(2026, 11, 16, 8, 0, 0);
        _register.Replace(Make.Summary(1, "ONGOING", supporters: 260_000, updated: later)); // crosses 100k and 250k
        _register.Replace(Make.Summary(2, "INSUFFICIENT_SUPPORT", supporters: 500, updated: later)); // collection failed
        _register.Replace(Make.Summary(3, "ONGOING", lastCall: true, updated: later)); // started, and last call
        _register.Summaries.Add(Make.Summary(6, "REGISTERED"));
        _register.Details[6] = () => Make.Details(Make.Summary(6, "REGISTERED"));
        _news.Items.Add(FakeNews.Item(3));

        var report = await Engine().SyncAsync("en");

        var changes = report.Changes.Select(c => (c.InitiativeId, c.Kind, c.OldValue, c.NewValue)).ToList();
        Assert.Equal(
            [
                (1, ChangeKind.SignatureMilestone, "90000", "250000"),
                (2, ChangeKind.StatusChanged, "ONGOING", "INSUFFICIENT_SUPPORT"),
                (3, ChangeKind.StatusChanged, "REGISTERED", "ONGOING"),
                (3, ChangeKind.LastCall, null, null),
                (6, ChangeKind.NewInitiative, null, "REGISTERED"),
            ],
            changes);
        Assert.All(report.Changes, c => Assert.Equal(Time.Now, c.DetectedAt));

        var pending = await Db.GetPendingChangesAsync();
        Assert.Equal(changes, pending.Select(c => (c.InitiativeId, c.Kind, c.OldValue, c.NewValue)));

        // Details are refreshed for the changed initiatives and fetched for the new one; 4 and 5 are untouched.
        Assert.Equal([1, 2, 3, 6], _register.DetailRequests.Order());
        Assert.Equal(1, report.NewsAdded);

        var failed = await Db.GetInitiativeAsync(2, "en");
        Assert.Equal(InitiativeOutcome.Failed, failed!.Outcome);
        Assert.False(failed.CanBeSigned);
    }

    [Fact]
    public async Task Changes_are_recorded_once()
    {
        SeedRegister();
        await Engine().SyncAsync("en");
        _register.Replace(Make.Summary(2, "INSUFFICIENT_SUPPORT", supporters: 500));

        await Engine().SyncAsync("en");
        var third = await Engine().SyncAsync("en");

        Assert.Empty(third.Changes);
        Assert.Single(await Db.GetPendingChangesAsync());
    }

    [Fact]
    public async Task A_broken_detail_record_does_not_stop_the_sync_and_is_retried()
    {
        SeedRegister();
        _register.BrokenDetails.Add(2);

        var first = await Engine().SyncAsync("en");

        Assert.Equal([2], first.DetailsFailed);
        Assert.Equal(2, first.DetailsFetched);
        Assert.False((await Db.GetInitiativeAsync(2, "en"))!.HasDetails);

        _register.BrokenDetails.Clear();
        _register.DetailRequests.Clear();
        var second = await Engine().SyncAsync("en");

        Assert.Equal([2], _register.DetailRequests);
        Assert.Empty(second.DetailsFailed);
        Assert.True((await Db.GetInitiativeAsync(2, "en"))!.HasDetails);
    }

    [Fact]
    public async Task Details_are_spread_over_several_syncs()
    {
        SeedRegister();

        var first = await Engine(maxDetails: 2).SyncAsync("en");
        var second = await Engine(maxDetails: 2).SyncAsync("en");

        Assert.Equal((2, 1), (first.DetailsFetched, first.DetailsPending));
        Assert.Equal((1, 0), (second.DetailsFetched, second.DetailsPending));
    }

    [Fact]
    public async Task News_failure_is_reported_but_the_register_is_saved()
    {
        SeedRegister();
        _news.Fails = true;

        var report = await Engine().SyncAsync("en");

        Assert.Null(report.NewsAdded);
        Assert.NotNull(report.NewsError);
        Assert.Equal(5, (await Db.GetInitiativesAsync("en")).Count);
        Assert.NotNull(await Db.GetLastSyncAsync());
    }

    [Fact]
    public async Task List_failure_fails_the_sync_and_writes_nothing()
    {
        SeedRegister();
        _register.ListFails = true;

        await Assert.ThrowsAsync<HttpRequestException>(() => Engine().SyncAsync("en"));

        Assert.Empty(await Db.GetInitiativesAsync("en"));
        Assert.Null(await Db.GetLastSyncAsync());
    }

    [Fact]
    public async Task Titles_follow_the_sync_language()
    {
        _register.Set(Make.Summary(5, "WITHDRAWN", title: "Αποσύρθηκε", language: "EL"));

        await Engine().SyncAsync("el-GR", includeNews: false);

        var item = await Db.GetInitiativeAsync(5, "el");
        Assert.Equal(("Αποσύρθηκε", "el"), (item!.Title, item.TitleLanguage));
    }

    [Fact]
    public async Task Details_of_finished_initiatives_are_fetched_on_demand_once()
    {
        SeedRegister();
        await Engine().SyncAsync("en");
        _register.DetailRequests.Clear();

        Assert.True(await Engine().EnsureDetailsAsync(4));
        Assert.False(await Engine().EnsureDetailsAsync(4));
        Assert.Equal([4], _register.DetailRequests);
        Assert.NotNull(await Db.GetDetailsAsync(4));
        await Assert.ThrowsAsync<ArgumentException>(() => Engine().EnsureDetailsAsync(999));
    }

    [Theory]
    [InlineData(99_999, 100_000, 100_000)]
    [InlineData(90_000, 760_000, 750_000)]
    [InlineData(999_000, 1_000_001, 1_000_000)]
    [InlineData(100_000, 120_000, 0)]
    [InlineData(1_000_000, 2_000_000, 0)]
    public void Only_the_highest_milestone_crossed_is_reported(long before, long after, long expected)
    {
        var snapshot = new InitiativeSnapshot { Id = 1, Year = "2026", Number = "000001", Status = EciStatus.Ongoing, TotalSupporters = before };

        var milestone = SyncEngine.DetectChanges(snapshot, Make.Summary(1, supporters: after), Time.Now)
            .SingleOrDefault(c => c.Kind == ChangeKind.SignatureMilestone);

        Assert.Equal(expected, milestone is null ? 0 : long.Parse(milestone.NewValue!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void An_unknown_new_status_is_not_reported_as_a_change()
    {
        var snapshot = new InitiativeSnapshot { Id = 1, Year = "2026", Number = "000001", Status = EciStatus.Ongoing };

        Assert.Empty(SyncEngine.DetectChanges(snapshot, Make.Summary(1, "SOMETHING_NEW"), Time.Now));
    }
}
