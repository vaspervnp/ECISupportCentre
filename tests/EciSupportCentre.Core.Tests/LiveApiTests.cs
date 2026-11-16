using EciSupportCentre.Core.Api;
using EciSupportCentre.Core.Models;

namespace EciSupportCentre.Core.Tests;

/// <summary>
/// Calls the real ECI services. They run only when <c>ECI_LIVE_TESTS=1</c>, so CI and normal runs stay offline:
/// <c>ECI_LIVE_TESTS=1 dotnet test --filter Category=Live</c>.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveApiTests : IDisposable
{
    private static readonly bool Enabled = Environment.GetEnvironmentVariable("ECI_LIVE_TESTS") == "1";

    private readonly HttpClient _http = EciHttp.CreateClient();

    public void Dispose() => _http.Dispose();

    [Fact]
    public async Task Register_lists_all_initiatives_with_known_statuses()
    {
        if (!Enabled)
        {
            return;
        }

        var client = new RegisterApiClient(_http);
        var all = await client.GetAllAsync("en");
        var stats = await client.GetStatisticsAsync();

        Assert.Equal(stats.Registered, all.Count);
        Assert.DoesNotContain(all, e => e.Status == EciStatus.Unknown);
    }

    [Fact]
    public async Task Details_are_available_for_an_ongoing_initiative()
    {
        if (!Enabled)
        {
            return;
        }

        var client = new RegisterApiClient(_http);
        // The ONGOING filter ignores offset and size and always returns every ongoing initiative.
        var page = await client.SearchAsync(RegisterSearchFilter.Ongoing, "el", 0, 1);
        Assert.Equal(page.RecordsFound, page.Entries.Count);
        var entry = page.Entries[0];

        var details = await client.GetDetailsAsync(entry.Year, entry.Number);

        Assert.Equal(entry.Id, details.Id);
        Assert.NotNull(details.Deadline);
        Assert.Equal(24, details.LinguisticVersions.Count);
    }

    [Fact]
    public async Task News_feed_has_items()
    {
        if (!Enabled)
        {
            return;
        }

        var news = await new NewsFeedClient(_http).GetLatestAsync("el");

        Assert.NotEmpty(news);
    }
}
