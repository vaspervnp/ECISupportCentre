using System.Text.Json;
using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Presentation;
using EciSupportCentre.Core.Serialization;
using EciSupportCentre.Core.Storage;

namespace EciSupportCentre.Core.Tests;

public class InitiativePresentationTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);

    private static InitiativeListItem Item(string status, DateOnly? start = null, DateOnly? end = null) => new()
    {
        Id = 1,
        PubRegNum = "ECI(2026)000001",
        Year = "2026",
        Number = "000001",
        Status = EciStatusExtensions.ParseStatus(status),
        Title = "T",
        TitleLanguage = "en",
        StartCollectionDate = start,
        CollectionEndDate = end,
    };

    private static InitiativeDetails Details(string sample) =>
        JsonSerializer.Deserialize(Samples.Read(sample), EciJsonContext.Default.InitiativeDetails)!;

    [Theory]
    [InlineData("REGISTERED", "2027-01-10", null, DateLineKind.StartsOn, "2027-01-10")]
    [InlineData("REGISTERED", "2026-11-01", null, DateLineKind.None, null)]
    [InlineData("REGISTERED", null, null, DateLineKind.None, null)]
    [InlineData("ONGOING", "2026-11-13", "2027-11-13", DateLineKind.EndsOn, "2027-11-13")]
    [InlineData("ONGOING", "2026-11-13", null, DateLineKind.None, null)]
    [InlineData("VERIFICATION", null, "2026-05-01", DateLineKind.EndedOn, "2026-05-01")]
    [InlineData("ANSWERED", null, "2025-07-31", DateLineKind.EndedOn, "2025-07-31")]
    [InlineData("INSUFFICIENT_SUPPORT", null, "2026-08-08", DateLineKind.EndedOn, "2026-08-08")]
    [InlineData("WITHDRAWN", null, "2026-08-08", DateLineKind.None, null)]
    public void Date_line_depends_on_the_phase(string status, string? start, string? end, DateLineKind kind, string? date)
    {
        var line = InitiativePresentation.DateLine(
            Item(status, start is null ? null : DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture), end is null ? null : DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture)),
            Today);

        Assert.Equal(kind, line.Kind);
        Assert.Equal(date is null ? null : DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), line.Date);
    }

    [Fact]
    public void Timeline_is_in_date_order()
    {
        var steps = InitiativePresentation.Timeline(Details("details_answered.json"));

        Assert.Equal(
            ["REGISTERED", "COLLECTION_START_DATE", "CLOSED", "VERIFICATION", "SUBMITTED", "ANSWERED"],
            steps.Select(s => s.Step));
    }

    [Fact]
    public void Timeline_puts_steps_without_date_last()
    {
        var steps = InitiativePresentation.Timeline(Details("details_withdrawn.json"));

        Assert.Equal("COLLECTION_START_DATE", steps[^1].Step);
        Assert.Null(steps[^1].Date);
    }

    [Fact]
    public void Top_countries_prefer_verified_signatures()
    {
        var top = InitiativePresentation.TopCountries(Details("details_answered.json"), 2);

        Assert.Equal(["ES", "AT"], top.Select(c => c.CountryCode));
        Assert.True(top[0].Total >= top[1].Total);
    }

    [Fact]
    public void Top_countries_use_collected_signatures_while_collecting()
    {
        var details = Details("details_ongoing.json");

        var top = InitiativePresentation.TopCountries(details, 10);

        Assert.NotEmpty(top);
        Assert.All(top, c => Assert.Contains(c, details.SosReport!.Countries));
    }

    [Theory]
    [InlineData("EL", "GR")]
    [InlineData("uk", "GB")]
    [InlineData("DE", "DE")]
    public void Eu_country_codes_map_to_iso(string eu, string iso) =>
        Assert.Equal(iso, InitiativePresentation.ToIsoCountry(eu));

    [Fact]
    public void Document_links_prefer_the_language()
    {
        var link = Details("details_answered.json").Answer!.Links[0];

        Assert.Contains("/LT/", InitiativePresentation.DocumentLink(link, "lt")!.ToString(), StringComparison.Ordinal);
        Assert.Contains("/GA/", InitiativePresentation.DocumentLink(link, "ga")!.ToString(), StringComparison.Ordinal);
        Assert.Equal(link.DefaultLink, InitiativePresentation.DocumentLink(link with { Translations = [] }, "lt"));
    }

    [Fact]
    public void Official_page_and_logo_urls()
    {
        Assert.Equal(
            new Uri("https://citizens-initiative.europa.eu/initiatives/details/2026/000009_el"),
            EciSources.InitiativePage("2026", "000009", "el-GR"));
        Assert.Equal(
            new Uri("https://register.eci.ec.europa.eu/core/api/register/logo/9186"),
            EciSources.Logo(9186));
    }
}
