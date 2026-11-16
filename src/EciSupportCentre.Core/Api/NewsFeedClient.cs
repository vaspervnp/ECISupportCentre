using System.Globalization;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using EciSupportCentre.Core.Models;

namespace EciSupportCentre.Core.Api;

public interface INewsFeedClient
{
    /// <summary>Latest news in <paramref name="language"/>; falls back to English if that feed is missing.</summary>
    Task<IReadOnlyList<NewsItem>> GetLatestAsync(string language, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the "Latest news" RSS 2.0 feed of citizens-initiative.europa.eu.
/// The feed exists per language (<c>rss_el</c>, <c>rss_de</c>, ...); untranslated articles appear in English.
/// </summary>
public sealed class NewsFeedClient(HttpClient httpClient) : INewsFeedClient
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public static Uri FeedUri(string? language) =>
        new($"https://citizens-initiative.europa.eu/node/418/rss_{EciSources.Normalize(language)}");

    public async Task<IReadOnlyList<NewsItem>> GetLatestAsync(string language, CancellationToken cancellationToken = default)
    {
        var code = EciSources.Normalize(language);
        try
        {
            return await FetchAsync(code, cancellationToken).ConfigureAwait(false);
        }
        catch (EciApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound && code != EciSources.DefaultLanguage)
        {
            return await FetchAsync(EciSources.DefaultLanguage, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<NewsItem>> FetchAsync(string language, CancellationToken cancellationToken)
    {
        var uri = FeedUri(language);
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new EciApiException(response.StatusCode, uri);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            try
            {
                var doc = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);
                return Parse(doc, language);
            }
            catch (XmlException ex)
            {
                throw new EciApiException($"Invalid RSS from {uri}: {ex.Message}", ex);
            }
        }
    }

    /// <summary>Parses an RSS 2.0 document into news items, newest first.</summary>
    public static IReadOnlyList<NewsItem> Parse(XDocument document, string language)
    {
        ArgumentNullException.ThrowIfNull(document);

        var channel = document.Root?.Element("channel");
        var feedLanguage = channel?.Element("language")?.Value is { Length: > 0 } l ? l : language;

        var items = new List<NewsItem>();
        foreach (var item in channel?.Elements("item") ?? [])
        {
            var title = Clean(item.Element("title")?.Value);
            var link = ToUri(item.Element("link")?.Value);
            var id = Clean(item.Element(Atom + "id")?.Value) ?? Clean(item.Element("guid")?.Value) ?? link?.ToString();
            if (title is null || id is null)
            {
                continue;
            }

            var enclosure = item.Element("enclosure");
            var isImage = enclosure?.Attribute("type")?.Value.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false;

            items.Add(new NewsItem
            {
                Id = id,
                Title = title,
                Link = link,
                Published = ParseRfc822(item.Element("pubDate")?.Value),
                DescriptionHtml = Clean(item.Element("description")?.Value),
                Category = Clean(item.Element("category")?.Value),
                ImageUrl = isImage ? ToUri(enclosure?.Attribute("url")?.Value) : null,
                Language = feedLanguage,
            });
        }

        return [.. items.OrderByDescending(i => i.Published ?? DateTimeOffset.MinValue)];
    }

    /// <summary>Parses dates like <c>Thu, 12 Nov 2026 00:00:00 +0100</c>.</summary>
    internal static DateTimeOffset? ParseRfc822(string? value)
    {
        value = Clean(value);
        if (value is null)
        {
            return null;
        }

        // .NET's "zzz" wants "+01:00"; RSS uses "+0100".
        if (value.Length > 5 && value[^5] is '+' or '-' && value[^3] != ':')
        {
            value = value[..^2] + ":" + value[^2..];
        }

        string[] formats = ["ddd, dd MMM yyyy HH:mm:ss zzz", "ddd, d MMM yyyy HH:mm:ss zzz", "dd MMM yyyy HH:mm:ss zzz"];
        if (DateTimeOffset.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var exact))
        {
            return exact;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var loose) ? loose : null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Uri? ToUri(string? value) =>
        Uri.TryCreate(Clean(value), UriKind.Absolute, out var uri) ? uri : null;
}
