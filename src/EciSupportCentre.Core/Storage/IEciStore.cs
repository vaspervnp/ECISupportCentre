using EciSupportCentre.Core.Models;

namespace EciSupportCentre.Core.Storage;

/// <summary>Local storage used by the sync engine. Implemented with SQLite in EciSupportCentre.Data.</summary>
public interface IEciStore
{
    Task<IReadOnlyDictionary<long, InitiativeSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the list entries (titles in <paramref name="language"/>) and the changes found, in one transaction.
    /// </summary>
    Task SaveSummariesAsync(
        string language,
        IReadOnlyList<InitiativeSummary> summaries,
        IReadOnlyList<InitiativeChange> changes,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>Stores the full record, including the titles of every language version.</summary>
    Task SaveDetailsAsync(InitiativeDetails details, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores news items and returns the articles never stored before in any language
    /// (so switching the app language does not make every article new).
    /// </summary>
    Task<IReadOnlyList<NewsItem>> SaveNewsAsync(IReadOnlyList<NewsItem> items, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<string?> GetStateAsync(string key, CancellationToken cancellationToken = default);

    Task SetStateAsync(string key, string? value, CancellationToken cancellationToken = default);
}

/// <summary>Keys of <see cref="IEciStore.GetStateAsync"/>.</summary>
public static class StateKeys
{
    /// <summary>UTC time of the last successful sync (ISO 8601).</summary>
    public const string LastSync = "last_sync_utc";

    /// <summary>Language of the titles fetched by the last sync.</summary>
    public const string LastSyncLanguage = "last_sync_language";

    /// <summary>Register statistics as JSON.</summary>
    public const string Statistics = "statistics";

    /// <summary>UTC time of the last successful news refresh.</summary>
    public const string LastNewsSync = "last_news_sync_utc";
}
