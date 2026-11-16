namespace EciSupportCentre.Core.Models;

/// <summary>One article of the ECI news feed.</summary>
public sealed record NewsItem
{
    /// <summary>Stable id (the article's node URL).</summary>
    public required string Id { get; init; }

    public required string Title { get; init; }

    public Uri? Link { get; init; }

    public DateTimeOffset? Published { get; init; }

    /// <summary>Summary as HTML.</summary>
    public string? DescriptionHtml { get; init; }

    public string? Category { get; init; }

    public Uri? ImageUrl { get; init; }

    /// <summary>Language of the feed the item came from.</summary>
    public required string Language { get; init; }

    /// <summary>The user opened the article (stored only on the device).</summary>
    public bool IsRead { get; init; }
}
