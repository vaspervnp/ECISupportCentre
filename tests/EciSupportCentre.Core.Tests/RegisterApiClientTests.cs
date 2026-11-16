using System.Globalization;
using System.Net;
using System.Text.Json;
using EciSupportCentre.Core.Api;
using EciSupportCentre.Core.Models;

namespace EciSupportCentre.Core.Tests;

public class RegisterApiClientTests
{
    private const string Base = "https://register.eci.ec.europa.eu/core/api/register/";

    private static (RegisterApiClient Client, FakeHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        return (new RegisterApiClient(new HttpClient(handler)), handler);
    }

    [Fact]
    public async Task Search_builds_the_url_and_parses_the_envelope()
    {
        var (client, handler) = Create(_ => Samples.Json("search_all_en.json"));

        var result = await client.SearchAsync(RegisterSearchFilter.All, "en-GB", 0, 20);

        Assert.Equal(new Uri(Base + "search/ALL/EN/0/20"), Assert.Single(handler.Requests));
        Assert.Equal(162, result.Requests);
        Assert.Equal(136, result.Registered);
        Assert.Equal(15, result.Successful);
        Assert.Equal(13, result.RecordsFound);
        Assert.Equal(13, result.Entries.Count);
    }

    [Fact]
    public async Task Search_parses_entries()
    {
        var (client, _) = Create(_ => Samples.Json("search_all_en.json"));

        var result = await client.SearchAsync(RegisterSearchFilter.All, "en", 0, 20);
        var first = result.Entries[0];

        Assert.Equal(9186, first.Id);
        Assert.Equal("ECI(2026)000009", first.PubRegNum);
        Assert.Equal("2026", first.Year);
        Assert.Equal("000009", first.Number);
        Assert.Equal(EciStatus.Ongoing, first.Status);
        Assert.Equal("All On Board - For your right to citizenship without borders", first.Title);
        Assert.Equal(new Uri("https://eci.ec.europa.eu/069/public/?lg=en"), first.SupportLink);
        Assert.Equal(new DateTime(2026, 11, 13, 0, 0, 0), first.LatestUpdateDate);
        Assert.False(first.LastCall);
        Assert.Equal(9186, first.Logo?.Id);
        Assert.True(first.TotalSupporters > 0);

        // Every status in the sample is known to the app.
        Assert.DoesNotContain(result.Entries, e => e.Status == EciStatus.Unknown);

        // Some old initiatives collected on paper only and have no support link or logo.
        Assert.Contains(result.Entries, e => e.SupportLink is null && e.Logo is null);
    }

    [Fact]
    public async Task Search_returns_titles_in_the_requested_language()
    {
        var (client, handler) = Create(_ => Samples.Json("search_all_el.json"));

        var result = await client.SearchAsync(RegisterSearchFilter.Ongoing, "el-GR", 0, 500);

        Assert.Equal(new Uri(Base + "search/ONGOING/EL/0/100"), Assert.Single(handler.Requests));
        Assert.All(result.Entries, e => Assert.Equal("EL", e.LanguageCode));
        Assert.StartsWith("Κάθε άνθρωπος δεκτός", result.Entries[0].Title, StringComparison.Ordinal);
        Assert.Equal(new Uri("https://eci.ec.europa.eu/069/public/?lg=el"), result.Entries[0].SupportLink);
    }

    [Fact]
    public async Task Unsupported_language_falls_back_to_english()
    {
        var (client, handler) = Create(_ => Samples.Json("search_all_en.json"));

        await client.SearchAsync(RegisterSearchFilter.Answered, "ja-JP", 40, 20);

        Assert.Equal(new Uri(Base + "search/ANSWERED/EN/40/20"), Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task GetAll_follows_every_page_by_offset()
    {
        const int total = 250;
        var (client, handler) = Create(request =>
        {
            // .../search/ALL/EN/{offset}/{size}
            var segments = request.RequestUri!.Segments;
            var offset = int.Parse(segments[^2].TrimEnd('/'), CultureInfo.InvariantCulture);
            var size = int.Parse(segments[^1], CultureInfo.InvariantCulture);
            var entries = Enumerable.Range(offset, Math.Max(0, Math.Min(size, total - offset)))
                .Select(i => new { id = 1000 + i, status = "WITHDRAWN", title = $"Initiative {i}" });
            return Samples.Text(JsonSerializer.Serialize(new { recordsFound = total, entries }));
        });

        var all = await client.GetAllAsync("de");

        Assert.Equal(total, all.Count);
        Assert.Equal(total, all.Select(e => e.Id).Distinct().Count());
        Assert.Equal(
            [new Uri(Base + "search/ALL/DE/0/100"), new Uri(Base + "search/ALL/DE/100/100"), new Uri(Base + "search/ALL/DE/200/100")],
            handler.Requests);
    }

    [Fact]
    public async Task GetAll_stops_on_an_empty_page()
    {
        var (client, handler) = Create(_ => Samples.Text("""{"recordsFound":500,"entries":[]}"""));

        var all = await client.GetAllAsync("en");

        Assert.Empty(all);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Details_of_an_ongoing_initiative()
    {
        var (client, handler) = Create(_ => Samples.Json("details_ongoing.json"));

        var details = await client.GetDetailsAsync("2026", "000009");

        Assert.Equal(new Uri(Base + "details/2026/000009"), Assert.Single(handler.Requests));
        Assert.Equal(9186, details.Id);
        Assert.Equal("ECI(2026)000009", details.ComRegNum);
        Assert.Equal(EciStatus.Ongoing, details.Status);
        Assert.Equal(InitiativePhase.Collecting, details.Phase);
        Assert.Equal(new DateOnly(2026, 7, 17), details.RegistrationDate);
        Assert.Equal(new DateOnly(2026, 11, 13), details.StartCollectionDate);
        Assert.Equal(new DateOnly(2027, 11, 13), details.Deadline);
        Assert.Equal(new DateOnly(2027, 11, 13), details.CollectionEndDate);
        Assert.Equal(134, details.TotalSignatures);
        Assert.NotEmpty(details.SosReport!.Countries);
        Assert.NotEmpty(details.Categories);
        Assert.Contains(details.Progress, p => p is { Step: "ONGOING", Active: true });

        Assert.Equal("EN", details.OriginalVersion?.LanguageCode);
        Assert.StartsWith("Κάθε άνθρωπος δεκτός", details.GetVersion("el")?.Title, StringComparison.Ordinal);
        Assert.StartsWith("Alle dabei", details.GetVersion("de-AT")?.Title, StringComparison.Ordinal);
        Assert.Equal("EN", details.GetVersion("fi")?.LanguageCode);
        Assert.NotNull(details.OriginalVersion?.Objectives);
        Assert.NotNull(details.OriginalVersion?.CommissionDecision?.Url);
    }

    [Fact]
    public async Task Details_of_an_answered_initiative()
    {
        var (client, _) = Create(_ => Samples.Json("details_answered.json"));

        var details = await client.GetDetailsAsync("2024", "000007");

        Assert.Equal(InitiativeOutcome.Successful, details.Outcome);
        Assert.Equal(new DateOnly(2026, 6, 16), details.Answer?.DecisionDate);
        Assert.Equal("COMMUNICATION", details.Answer?.Links[0].DefaultName);
        Assert.NotEmpty(details.Answer!.Links[0].Translations);
        Assert.Equal(1_294_188, details.Submission?.TotalSignatures);
        Assert.Equal(1_294_188, details.TotalSignatures);
        Assert.Null(details.Deadline);
        Assert.Equal(new DateOnly(2025, 7, 31), details.CollectionEndDate);
        Assert.Equal(24000m, details.Funding?.TotalAmount);
    }

    [Fact]
    public async Task Details_of_an_initiative_closed_early_and_submitted()
    {
        var (client, _) = Create(_ => Samples.Json("details_submitted.json"));

        var details = await client.GetDetailsAsync("2025", "000005");

        Assert.Equal(InitiativePhase.Submitted, details.Phase);
        Assert.Equal(new DateOnly(2026, 7, 15), details.EarlyClosureDate);
        Assert.Equal(new DateOnly(2026, 7, 15), details.CollectionEndDate);
        Assert.Contains(details.Progress, p => p.FootnoteType == "COLLECTION_EARLY_CLOSURE");
        Assert.NotEmpty(details.PreAnswer!.Links);
    }

    [Fact]
    public async Task Details_of_a_failed_initiative()
    {
        var (client, _) = Create(_ => Samples.Json("details_insufficient_support.json"));

        var details = await client.GetDetailsAsync("2025", "000003");

        Assert.Equal(InitiativeOutcome.Failed, details.Outcome);
        Assert.Null(details.Deadline);
        Assert.Equal(new DateOnly(2026, 8, 8), details.CollectionEndDate);
    }

    [Theory]
    [InlineData("details_registered.json", EciStatus.Registered)]
    [InlineData("details_ongoing.json", EciStatus.Ongoing)]
    [InlineData("details_verification.json", EciStatus.Verification)]
    [InlineData("details_submitted.json", EciStatus.Submitted)]
    [InlineData("details_answered.json", EciStatus.Answered)]
    [InlineData("details_insufficient_support.json", EciStatus.InsufficientSupport)]
    [InlineData("details_insufficient_support_after_verification.json", EciStatus.InsufficientSupportAfterVerification)]
    [InlineData("details_withdrawn.json", EciStatus.Withdrawn)]
    public async Task Details_parse_for_every_status(string sample, EciStatus expected)
    {
        var (client, _) = Create(_ => Samples.Json(sample));

        var details = await client.GetDetailsAsync("2020", "000001");

        Assert.Equal(expected, details.Status);
        Assert.NotNull(details.RegistrationDate);
        Assert.NotNull(details.OriginalVersion);
        Assert.NotEmpty(details.Progress);
    }

    [Fact]
    public async Task Statistics_are_parsed()
    {
        var (client, handler) = Create(_ => Samples.Json("statistics.json"));

        var stats = await client.GetStatisticsAsync();

        Assert.Equal(new Uri(Base + "statistics"), Assert.Single(handler.Requests));
        Assert.Equal(136, stats.Registered);
        Assert.Equal(26, stats.Rejected);
        Assert.Equal(76, stats.Unsuccessful);
        Assert.Equal(2, stats.StartingSoon);
    }

    [Fact]
    public async Task Logo_is_returned_as_bytes()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47];
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) });

        var bytes = await client.GetLogoAsync(9186);

        Assert.Equal(new Uri(Base + "logo/9186"), Assert.Single(handler.Requests));
        Assert.Equal(png, bytes);
    }

    [Fact]
    public async Task Http_errors_become_EciApiException()
    {
        var (client, _) = Create(_ => Samples.Text("", status: HttpStatusCode.BadRequest));

        var ex = await Assert.ThrowsAsync<EciApiException>(() => client.GetDetailsAsync("2026", "999999"));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(new Uri(Base + "details/2026/999999"), ex.RequestUri);
    }

    [Fact]
    public async Task Malformed_json_becomes_EciApiException()
    {
        var (client, _) = Create(_ => Samples.Text("<html>maintenance</html>", "text/html"));

        await Assert.ThrowsAsync<EciApiException>(() => client.GetStatisticsAsync());
    }

    [Fact]
    public async Task Empty_dates_are_null()
    {
        var (client, _) = Create(_ => Samples.Text("""{"id":1,"status":"REGISTERED","deadline":"","startCollectionDate":null,"latestUpdateDate":"bad"}"""));

        var details = await client.GetDetailsAsync("2026", "000001");

        Assert.Null(details.Deadline);
        Assert.Null(details.StartCollectionDate);
        Assert.Null(details.LatestUpdateDate);
        Assert.Null(details.CollectionEndDate);
        Assert.Null(details.OriginalVersion);
    }
}
