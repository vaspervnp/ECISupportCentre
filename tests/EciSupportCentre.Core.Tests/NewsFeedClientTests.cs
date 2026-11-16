using System.Net;
using EciSupportCentre.Core.Api;

namespace EciSupportCentre.Core.Tests;

public class NewsFeedClientTests
{
    [Fact]
    public async Task Parses_the_english_feed()
    {
        var handler = new FakeHandler(_ => Samples.Text(Samples.Read("news_en.xml"), "application/rss+xml"));
        var client = new NewsFeedClient(new HttpClient(handler));

        var news = await client.GetLatestAsync("en");

        Assert.Equal(new Uri("https://citizens-initiative.europa.eu/node/418/rss_en"), Assert.Single(handler.Requests));
        Assert.Equal(3, news.Count);

        var first = news[0];
        Assert.Equal("15th valid initiative submitted to the Commission for examination", first.Title);
        Assert.Equal("https://citizens-initiative.europa.eu/node/460", first.Id);
        Assert.Equal(new Uri("https://citizens-initiative.europa.eu/news/15th-valid-initiative-submitted-commission-examination-2026-11-12_en"), first.Link);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.FromHours(1)), first.Published);
        Assert.Equal("Secretariat-General", first.Category);
        Assert.NotNull(first.ImageUrl);
        Assert.Contains("<p>", first.DescriptionHtml, StringComparison.Ordinal);
        Assert.Equal("en", first.Language);

        Assert.True(news.Zip(news.Skip(1)).All(p => p.First.Published >= p.Second.Published), "Newest first");
    }

    [Fact]
    public async Task Uses_the_feed_of_the_requested_language()
    {
        var handler = new FakeHandler(_ => Samples.Text(Samples.Read("news_el.xml"), "application/rss+xml"));
        var client = new NewsFeedClient(new HttpClient(handler));

        var news = await client.GetLatestAsync("el-GR");

        Assert.Equal(new Uri("https://citizens-initiative.europa.eu/node/418/rss_el"), Assert.Single(handler.Requests));
        Assert.All(news, n => Assert.Equal("el", n.Language));
    }

    [Fact]
    public async Task Falls_back_to_english_when_the_language_feed_is_missing()
    {
        var handler = new FakeHandler(request => request.RequestUri!.AbsoluteUri.EndsWith("_en", StringComparison.Ordinal)
            ? Samples.Text(Samples.Read("news_en.xml"), "application/rss+xml")
            : Samples.Text("", "text/html", HttpStatusCode.NotFound));
        var client = new NewsFeedClient(new HttpClient(handler));

        var news = await client.GetLatestAsync("ga");

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(3, news.Count);
        Assert.All(news, n => Assert.Equal("en", n.Language));
    }

    [Fact]
    public async Task Other_errors_are_reported()
    {
        var handler = new FakeHandler(_ => Samples.Text("", "text/html", HttpStatusCode.ServiceUnavailable));
        var client = new NewsFeedClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<EciApiException>(() => client.GetLatestAsync("de"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
    }

    [Fact]
    public async Task Invalid_xml_is_reported()
    {
        var handler = new FakeHandler(_ => Samples.Text("<rss><channel>", "application/rss+xml"));
        var client = new NewsFeedClient(new HttpClient(handler));

        await Assert.ThrowsAsync<EciApiException>(() => client.GetLatestAsync("en"));
    }

    [Theory]
    [InlineData("Thu, 12 Nov 2026 00:00:00 +0100", 2026, 11, 12, 0, 1)]
    [InlineData("Tue, 6 Oct 2026 14:00:00 +0200", 2026, 10, 6, 14, 2)]
    [InlineData("Tue, 01 Sep 2026 08:00:00 -0000", 2026, 9, 1, 8, 0)]
    public void Parses_rss_dates(string value, int y, int m, int d, int h, int offsetHours) =>
        Assert.Equal(new DateTimeOffset(y, m, d, h, 0, 0, TimeSpan.FromHours(offsetHours)), NewsFeedClient.ParseRfc822(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("yesterday")]
    public void Bad_rss_dates_are_null(string? value) => Assert.Null(NewsFeedClient.ParseRfc822(value));
}
