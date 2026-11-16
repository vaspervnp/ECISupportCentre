using System.Globalization;
using System.Text.Json;
using EciSupportCentre.Core.Api;
using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Serialization;
using EciSupportCentre.Core.Storage;

namespace EciSupportCentre.Core.Sync;

/// <summary>
/// Brings the local store up to date with the register and the news feed, and records what changed.
/// </summary>
/// <remarks>
/// <para>The list (about 50 KB) is fetched on every sync. Details (about 230 KB each) are fetched only when needed:
/// for initiatives that are still active and have none yet, and for any initiative whose <c>latestUpdateDate</c>
/// or status changed. Details of finished initiatives are fetched on demand with <see cref="EnsureDetailsAsync"/>.</para>
/// <para>The first sync records no changes, so a fresh install does not notify about the whole register.</para>
/// </remarks>
public sealed class SyncEngine(
    IRegisterApiClient register,
    INewsFeedClient news,
    IEciStore store,
    TimeProvider? timeProvider = null)
{
    /// <summary>Supporter counts that produce a <see cref="ChangeKind.SignatureMilestone"/>.</summary>
    public static IReadOnlyList<long> SignatureMilestones { get; } = [100_000, 250_000, 500_000, 750_000, 1_000_000];

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    /// <summary>Most details fetched in one sync; the rest follow in later syncs. Keeps a sync short and polite.</summary>
    public int MaxDetailsPerSync { get; init; } = 30;

    public async Task<SyncReport> SyncAsync(string language, bool includeNews = true, CancellationToken cancellationToken = default)
    {
        var lang = EciSources.Normalize(language);
        var started = _time.GetUtcNow();

        // 1. List and statistics. If this fails the sync fails: nothing is written.
        var summaries = await register.GetAllAsync(lang, cancellationToken).ConfigureAwait(false);
        var statistics = await register.GetStatisticsAsync(cancellationToken).ConfigureAwait(false);

        // 2. Compare with what we have.
        var snapshots = await store.GetSnapshotsAsync(cancellationToken).ConfigureAwait(false);
        var firstSync = snapshots.Count == 0;
        var now = _time.GetUtcNow();

        var changes = new List<InitiativeChange>();
        var needDetails = new List<InitiativeSummary>();
        foreach (var summary in summaries)
        {
            var snapshot = snapshots.GetValueOrDefault(summary.Id);
            if (!firstSync)
            {
                changes.AddRange(DetectChanges(snapshot, summary, now));
            }

            if (NeedsDetails(snapshot, summary))
            {
                needDetails.Add(summary);
            }
        }

        await store.SaveSummariesAsync(lang, summaries, changes, now, cancellationToken).ConfigureAwait(false);
        await store.SetStateAsync(StateKeys.Statistics, JsonSerializer.Serialize(statistics, EciJsonContext.Default.RegisterStatistics), cancellationToken).ConfigureAwait(false);

        // 3. Details: active initiatives first, most recently updated first.
        var queue = needDetails
            .OrderBy(s => IsActive(s.Status) ? 0 : 1)
            .ThenByDescending(s => s.LatestUpdateDate ?? DateTime.MinValue)
            .ToList();

        var fetched = 0;
        var failed = new List<long>();
        foreach (var summary in queue.Take(MaxDetailsPerSync))
        {
            try
            {
                var details = await register.GetDetailsAsync(summary.Year, summary.Number, cancellationToken).ConfigureAwait(false);
                await store.SaveDetailsAsync(details, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                fetched++;
            }
            catch (Exception ex) when (ex is EciApiException or HttpRequestException)
            {
                // One broken record must not stop the sync; it is retried next time.
                failed.Add(summary.Id);
            }
        }

        // 4. News. A failure is reported, not thrown: the register data is already saved.
        IReadOnlyList<NewsItem>? newArticles = null;
        string? newsError = null;
        if (includeNews)
        {
            try
            {
                var items = await news.GetLatestAsync(lang, cancellationToken).ConfigureAwait(false);
                newArticles = await store.SaveNewsAsync(items, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                await store.SetStateAsync(StateKeys.LastNewsSync, Iso(_time.GetUtcNow()), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is EciApiException or HttpRequestException)
            {
                newsError = ex.Message;
            }
        }

        var finished = _time.GetUtcNow();
        await store.SetStateAsync(StateKeys.LastSync, Iso(finished), cancellationToken).ConfigureAwait(false);
        await store.SetStateAsync(StateKeys.LastSyncLanguage, lang, cancellationToken).ConfigureAwait(false);

        return new SyncReport
        {
            Language = lang,
            StartedAt = started,
            FinishedAt = finished,
            FirstSync = firstSync,
            InitiativeCount = summaries.Count,
            Changes = changes,
            DetailsFetched = fetched,
            DetailsFailed = failed,
            DetailsPending = Math.Max(0, queue.Count - MaxDetailsPerSync),
            NewArticles = newArticles,
            NewsError = newsError,
        };
    }

    /// <summary>
    /// Fetches and stores the details of one initiative when they are missing or older than the list entry.
    /// Returns false when nothing had to be fetched.
    /// </summary>
    public async Task<bool> EnsureDetailsAsync(long initiativeId, CancellationToken cancellationToken = default)
    {
        var snapshots = await store.GetSnapshotsAsync(cancellationToken).ConfigureAwait(false);
        if (!snapshots.TryGetValue(initiativeId, out var snapshot))
        {
            throw new ArgumentException($"Initiative {initiativeId} is not stored.", nameof(initiativeId));
        }

        if (snapshot.HasDetails && snapshot.DetailsLatestUpdate == snapshot.LatestUpdateDate)
        {
            return false;
        }

        var details = await register.GetDetailsAsync(snapshot.Year, snapshot.Number, cancellationToken).ConfigureAwait(false);
        await store.SaveDetailsAsync(details, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Changes between the stored state and a fresh list entry.</summary>
    public static IEnumerable<InitiativeChange> DetectChanges(InitiativeSnapshot? before, InitiativeSummary after, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(after);

        if (before is null)
        {
            yield return Change(ChangeKind.NewInitiative, null, after.StatusCode);
            yield break;
        }

        if (before.Status != after.Status && after.Status != EciStatus.Unknown)
        {
            yield return Change(ChangeKind.StatusChanged, StatusCode(before.Status), after.StatusCode);
        }

        if (after.LastCall && !before.LastCall)
        {
            yield return Change(ChangeKind.LastCall, null, null);
        }

        // Only the highest milestone crossed since the last sync.
        var crossed = SignatureMilestones.LastOrDefault(m => before.TotalSupporters < m && after.TotalSupporters >= m);
        if (crossed > 0)
        {
            yield return Change(
                ChangeKind.SignatureMilestone,
                before.TotalSupporters.ToString(CultureInfo.InvariantCulture),
                crossed.ToString(CultureInfo.InvariantCulture));
        }

        InitiativeChange Change(ChangeKind kind, string? oldValue, string? newValue) => new()
        {
            InitiativeId = after.Id,
            Kind = kind,
            OldValue = oldValue,
            NewValue = newValue,
            DetectedAt = now,
        };
    }

    /// <summary>Whether a sync should fetch the details of this initiative.</summary>
    public static bool NeedsDetails(InitiativeSnapshot? before, InitiativeSummary after)
    {
        ArgumentNullException.ThrowIfNull(after);

        if (before is null || !before.HasDetails)
        {
            // Finished initiatives rarely change; their details are fetched when the user opens them.
            return IsActive(after.Status);
        }

        return before.Status != after.Status || before.DetailsLatestUpdate != after.LatestUpdateDate;
    }

    /// <summary>Statuses whose initiatives still change: dates, signatures, verification, answer.</summary>
    public static bool IsActive(EciStatus status) => status is
        EciStatus.Registered or EciStatus.Ongoing or EciStatus.Closed or EciStatus.Verification or EciStatus.Submitted;

    private static string StatusCode(EciStatus status) => status switch
    {
        EciStatus.InsufficientSupport => "INSUFFICIENT_SUPPORT",
        EciStatus.InsufficientSupportAfterVerification => "INSUFFICIENT_SUPPORT_AFTER_VERIFICATION",
        _ => status.ToString().ToUpperInvariant(),
    };

    private static string Iso(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}

/// <summary>Result of one <see cref="SyncEngine.SyncAsync"/>.</summary>
public sealed record SyncReport
{
    public required string Language { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset FinishedAt { get; init; }

    /// <summary>True when the store was empty; no changes are recorded then.</summary>
    public bool FirstSync { get; init; }

    public int InitiativeCount { get; init; }

    public IReadOnlyList<InitiativeChange> Changes { get; init; } = [];

    public int DetailsFetched { get; init; }

    /// <summary>Initiatives whose details could not be fetched; retried next sync.</summary>
    public IReadOnlyList<long> DetailsFailed { get; init; } = [];

    /// <summary>Details left for later syncs because of <see cref="SyncEngine.MaxDetailsPerSync"/>.</summary>
    public int DetailsPending { get; init; }

    /// <summary>Articles never stored before, newest first; null when news were skipped or failed.</summary>
    public IReadOnlyList<NewsItem>? NewArticles { get; init; }

    /// <summary>Number of <see cref="NewArticles"/>; null when news were skipped or failed.</summary>
    public int? NewsAdded => NewArticles?.Count;

    public string? NewsError { get; init; }
}
