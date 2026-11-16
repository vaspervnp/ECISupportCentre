using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Storage;
using Microsoft.Data.Sqlite;

namespace EciSupportCentre.Data.Tests;

public class EciDatabaseTests : DatabaseTest
{
    [Fact]
    public async Task Initialize_creates_the_latest_schema_and_is_repeatable()
    {
        Assert.Equal(Schema.CurrentVersion, await Db.GetSchemaVersionAsync());

        await Db.InitializeAsync();

        Assert.Equal(Schema.CurrentVersion, await Db.GetSchemaVersionAsync());
    }

    [Fact]
    public async Task A_database_from_a_newer_app_is_refused()
    {
        await using (var connection = new SqliteConnection($"Data Source={Db.Path}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA user_version = {Schema.CurrentVersion + 1};";
            await command.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => Db.InitializeAsync());
    }

    [Fact]
    public async Task Summaries_are_stored_and_read_back()
    {
        var summary = Make.Summary(1, supporters: 4242, lastCall: true) with { Logo = new EciFile { Id = 77 } };

        await Db.SaveSummariesAsync("en", [summary], [], Time.Now);
        var item = Assert.Single(await Db.GetInitiativesAsync("en"));

        Assert.Equal(1, item.Id);
        Assert.Equal("ECI(2026)000001", item.PubRegNum);
        Assert.Equal(EciStatus.Ongoing, item.Status);
        Assert.Equal(InitiativePhase.Collecting, item.Phase);
        Assert.Equal("Initiative 1 (EN)", item.Title);
        Assert.Equal("en", item.TitleLanguage);
        Assert.Equal(4242, item.TotalSupporters);
        Assert.True(item.LastCall);
        Assert.Equal(77, item.LogoId);
        Assert.Equal(new DateTime(2026, 11, 1, 10, 0, 0), item.LatestUpdateDate);
        Assert.True(item.CanBeSigned);
        Assert.False(item.HasDetails);
        Assert.False(item.IsFollowed);
    }

    [Fact]
    public async Task Titles_fall_back_to_english_then_the_original_language()
    {
        await Db.SaveSummariesAsync("en", [Make.Summary(1)], [], Time.Now);
        await Db.SaveSummariesAsync("el", [Make.Summary(1, title: "Πρωτοβουλία ένα", language: "EL")], [], Time.Now);

        var greek = await Db.GetInitiativeAsync(1, "el-GR");
        var finnish = await Db.GetInitiativeAsync(1, "fi");

        Assert.Equal(("Πρωτοβουλία ένα", "el"), (greek!.Title, greek.TitleLanguage));
        Assert.Equal(new Uri("https://eci.ec.europa.eu/001/public/?lg=el"), greek.SupportLink);
        Assert.Equal(("Initiative 1 (EN)", "en"), (finnish!.Title, finnish.TitleLanguage));
    }

    [Fact]
    public async Task Title_falls_back_to_the_original_language_when_there_is_no_english()
    {
        var details = Make.Details(Make.Summary(5)) with
        {
            LinguisticVersions = [new LinguisticVersion { Original = true, LanguageCode = "DE", Title = "Initiative fünf" }],
        };

        await Db.SaveDetailsAsync(details, Time.Now);
        var item = await Db.GetInitiativeAsync(5, "fr");

        Assert.Equal(("Initiative fünf", "de"), (item!.Title, item.TitleLanguage));
    }

    [Fact]
    public async Task Details_are_stored_with_dates_and_every_language()
    {
        var details = Samples.Details("details_ongoing.json");
        await Db.SaveSummariesAsync("en", [Make.Summary(details.Id, title: "Old title")], [], Time.Now);

        await Db.SaveDetailsAsync(details, Time.Now);

        var item = await Db.GetInitiativeAsync(details.Id, "el");
        Assert.True(item!.HasDetails);
        Assert.StartsWith("Κάθε άνθρωπος δεκτός", item.Title, StringComparison.Ordinal);
        Assert.Equal(new DateOnly(2026, 7, 17), item.RegistrationDate);
        Assert.Equal(new DateOnly(2026, 11, 13), item.StartCollectionDate);
        Assert.Equal(new DateOnly(2027, 11, 13), item.CollectionEndDate);

        var english = await Db.GetInitiativeAsync(details.Id, "en");
        Assert.Equal("All On Board - For your right to citizenship without borders", english!.Title);

        var stored = await Db.GetDetailsAsync(details.Id);
        Assert.NotNull(stored);
        Assert.Equal(details.ComRegNum, stored.ComRegNum);
        Assert.Equal(details.Deadline, stored.Deadline);
        Assert.Equal(details.LatestUpdateDate, stored.LatestUpdateDate);
        Assert.Equal(details.TotalSignatures, stored.TotalSignatures);
        Assert.Equal(details.LinguisticVersions.Count, stored.LinguisticVersions.Count);
        Assert.Equal(details.Progress, stored.Progress);
    }

    [Fact]
    public async Task Details_without_a_list_entry_create_the_initiative()
    {
        var details = Samples.Details("details_answered.json");

        await Db.SaveDetailsAsync(details, Time.Now);

        var item = await Db.GetInitiativeAsync(details.Id, "en");
        Assert.Equal(("2024", "000007"), (item!.Year, item.Number));
        Assert.Equal(EciStatus.Answered, item.Status);
    }

    [Theory]
    [InlineData("ECI(2026)000009", "2026", "000009")]
    [InlineData("weird", "", "weird")]
    public void Registration_numbers_are_split(string regNum, string year, string number) =>
        Assert.Equal((year, number), EciDatabase.ParseRegNum(regNum));

    [Fact]
    public async Task Snapshots_reflect_stored_state()
    {
        var summary = Make.Summary(1, supporters: 10);
        await Db.SaveSummariesAsync("en", [summary, Make.Summary(2, "WITHDRAWN")], [], Time.Now);
        await Db.SaveDetailsAsync(Make.Details(summary), Time.Now);

        var snapshots = await Db.GetSnapshotsAsync();

        Assert.Equal(2, snapshots.Count);
        Assert.True(snapshots[1].HasDetails);
        Assert.Equal(summary.LatestUpdateDate, snapshots[1].DetailsLatestUpdate);
        Assert.Equal(10, snapshots[1].TotalSupporters);
        Assert.False(snapshots[2].HasDetails);
        Assert.Equal(EciStatus.Withdrawn, snapshots[2].Status);
    }

    [Fact]
    public async Task Following_is_stored_and_filterable()
    {
        await Db.SaveSummariesAsync("en", [Make.Summary(1), Make.Summary(2)], [], Time.Now);

        await Db.SetFollowedAsync(2, true, Time.Now);
        await Db.SetFollowedAsync(2, true, Time.Now);
        var followed = await Db.QueryInitiativesAsync(new InitiativeQuery { FollowedOnly = true });

        Assert.Equal(2, Assert.Single(followed).Id);

        await Db.SetFollowedAsync(2, false, Time.Now);
        Assert.Empty(await Db.QueryInitiativesAsync(new InitiativeQuery { FollowedOnly = true }));
    }

    [Fact]
    public async Task Signed_mark_is_stored()
    {
        await Db.SaveSummariesAsync("en", [Make.Summary(1), Make.Summary(2)], [], Time.Now);

        await Db.SetSignedAsync(1, true, Time.Now);
        await Db.SetSignedAsync(1, true, Time.Now);

        Assert.True((await Db.GetInitiativeAsync(1, "en"))!.IsSigned);
        Assert.False((await Db.GetInitiativeAsync(2, "en"))!.IsSigned);

        await Db.SetSignedAsync(1, false, Time.Now);
        Assert.False((await Db.GetInitiativeAsync(1, "en"))!.IsSigned);
    }

    [Fact]
    public async Task A_version_1_database_is_upgraded_without_losing_data()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Db.Path)!, "v1.db");
        await using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = Schema.Migrations[0] + """
                PRAGMA user_version = 1;
                INSERT INTO initiative (id, pub_reg_num, year, number, status, first_seen_utc, updated_utc)
                VALUES (7, 'ECI(2026)000007', '2026', '000007', 'ONGOING', '2026-11-01T00:00:00.000Z', '2026-11-01T00:00:00.000Z');
                INSERT INTO initiative_text (initiative_id, language, title) VALUES (7, 'en', 'Old');
                INSERT INTO follow (initiative_id, followed_utc) VALUES (7, '2026-11-01T00:00:00.000Z');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var upgraded = new EciDatabase(path);
        await upgraded.InitializeAsync();
        await upgraded.SetSignedAsync(7, true, Time.Now);

        Assert.Equal(2, await upgraded.GetSchemaVersionAsync());
        var item = await upgraded.GetInitiativeAsync(7, "en");
        Assert.Equal(("Old", true, true), (item!.Title, item.IsFollowed, item.IsSigned));
    }

    [Theory]
    [InlineData("ONGOING", "2026-11-26", 10)]
    [InlineData("ONGOING", "2026-11-16", 0)]
    [InlineData("ONGOING", "2026-11-15", null)]
    [InlineData("ONGOING", null, null)]
    [InlineData("ANSWERED", "2026-11-26", null)]
    public void Days_left_only_while_collecting(string status, string? end, int? expected)
    {
        var item = new InitiativeListItem
        {
            Id = 1,
            PubRegNum = "",
            Year = "",
            Number = "",
            Title = "",
            TitleLanguage = "en",
            Status = EciStatusExtensions.ParseStatus(status),
            CollectionEndDate = end is null ? null : DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture),
        };

        Assert.Equal(expected, item.DaysLeft(new DateOnly(2026, 11, 16)));
    }

    [Fact]
    public async Task Only_official_links_can_be_signed()
    {
        await Db.SaveSummariesAsync("en", [Make.Summary(1) with { SupportLink = new Uri("https://example.org/sign") }, Make.Summary(2)], [], Time.Now);

        Assert.False((await Db.GetInitiativeAsync(1, "en"))!.CanBeSigned);
        Assert.True((await Db.GetInitiativeAsync(2, "en"))!.CanBeSigned);
    }

    [Fact]
    public async Task Changes_stay_pending_until_notified()
    {
        await Db.SaveSummariesAsync(
            "en",
            [Make.Summary(1)],
            [
                new InitiativeChange { InitiativeId = 1, Kind = ChangeKind.NewInitiative, NewValue = "REGISTERED", DetectedAt = Time.Now },
                new InitiativeChange { InitiativeId = 1, Kind = ChangeKind.StatusChanged, OldValue = "REGISTERED", NewValue = "ONGOING", DetectedAt = Time.Now },
            ],
            Time.Now);

        var pending = await Db.GetPendingChangesAsync();
        Assert.Equal([ChangeKind.NewInitiative, ChangeKind.StatusChanged], pending.Select(c => c.Kind));
        Assert.Equal(EciStatus.Ongoing, pending[1].NewStatus);
        Assert.Equal(Time.Now, pending[0].DetectedAt);

        await Db.MarkChangesNotifiedAsync([pending[0].Id]);

        Assert.Equal(ChangeKind.StatusChanged, Assert.Single(await Db.GetPendingChangesAsync()).Kind);
        Assert.Equal(2, (await Db.GetChangesAsync(1)).Count);
    }

    [Fact]
    public async Task News_reports_new_items_and_reads_newest_first()
    {
        var first = await Db.SaveNewsAsync([FakeNews.Item(3), FakeNews.Item(12), FakeNews.Item(7)], Time.Now);
        var again = await Db.SaveNewsAsync([FakeNews.Item(12) with { Title = "Edited" }, FakeNews.Item(14)], Time.Now);
        var greek = await Db.SaveNewsAsync([FakeNews.Item(12, "el")], Time.Now);

        Assert.Equal((3, 1, 1), (first, again, greek));

        var news = await Db.GetNewsAsync("en");
        Assert.Equal(["News 14", "Edited", "News 7", "News 3"], news.Select(n => n.Title));
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.FromHours(1)), news[0].Published);
        Assert.Single(await Db.GetNewsAsync("el"));
        Assert.Equal(2, (await Db.GetNewsAsync("en", limit: 2)).Count);
    }

    [Fact]
    public async Task News_falls_back_to_English_when_nothing_is_stored_in_the_language()
    {
        Assert.Empty(await Db.GetNewsAsync("de"));

        await Db.SaveNewsAsync([FakeNews.Item(1), FakeNews.Item(2)], Time.Now);
        await Db.SaveNewsAsync([FakeNews.Item(2, "el")], Time.Now);

        Assert.Equal(["el"], (await Db.GetNewsAsync("el")).Select(n => n.Language));
        Assert.Equal(["en", "en"], (await Db.GetNewsAsync("mt")).Select(n => n.Language));
    }

    [Fact]
    public async Task Read_mark_is_kept_across_languages_and_refreshes()
    {
        await Db.SaveNewsAsync([FakeNews.Item(1), FakeNews.Item(2), FakeNews.Item(2, "el")], Time.Now);

        await Db.SetNewsReadAsync(FakeNews.Item(2).Id);
        await Db.SaveNewsAsync([FakeNews.Item(2) with { Title = "Edited" }], Time.Now);

        var english = await Db.GetNewsAsync("en");
        Assert.Equal([("Edited", true), ("News 1", false)], english.Select(n => (n.Title, n.IsRead)));
        Assert.True(Assert.Single(await Db.GetNewsAsync("el")).IsRead);
    }

    [Fact]
    public async Task State_values_are_stored()
    {
        Assert.Null(await Db.GetStateAsync("x"));

        await Db.SetStateAsync("x", "1");
        await Db.SetStateAsync("x", "2");

        Assert.Equal("2", await Db.GetStateAsync("x"));
        Assert.Null(await Db.GetLastSyncAsync());
        Assert.Null(await Db.GetStatisticsAsync());
    }

    [Fact]
    public async Task Data_survives_reopening()
    {
        await Db.SaveSummariesAsync("en", [Make.Summary(1)], [], Time.Now);
        SqliteConnection.ClearAllPools();

        var reopened = new EciDatabase(Db.Path);
        await reopened.InitializeAsync();

        Assert.Single(await reopened.GetInitiativesAsync("en"));
    }
}
