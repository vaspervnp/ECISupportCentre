using System.Net;
using System.Text;
using System.Text.Json;
using EciSupportCentre.Core.Api;
using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Serialization;
using Microsoft.Data.Sqlite;

namespace EciSupportCentre.Data.Tests;

/// <summary>A fresh database file per test, deleted afterwards.</summary>
public abstract class DatabaseTest : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "eci-tests", Guid.NewGuid().ToString("N"));

    protected DatabaseTest()
    {
        Directory.CreateDirectory(_directory);
        Db = new EciDatabase(Path.Combine(_directory, "eci.db"));
    }

    protected EciDatabase Db { get; }

    protected ManualTime Time { get; } = new(new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero));

    public Task InitializeAsync() => Db.InitializeAsync();

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp file is harmless.
        }

        return Task.CompletedTask;
    }
}

public sealed class ManualTime(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}

internal static class Samples
{
    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", name), Encoding.UTF8);

    public static InitiativeDetails Details(string name) =>
        JsonSerializer.Deserialize(Read(name), EciJsonContext.Default.InitiativeDetails)!;
}

internal static class Make
{
    public static InitiativeSummary Summary(
        long id,
        string status = "ONGOING",
        long supporters = 1000,
        string? title = null,
        string language = "EN",
        DateTime? updated = null,
        bool lastCall = false) => new()
        {
            Id = id,
            PubRegNum = $"ECI(2026){id:000000}",
            Year = "2026",
            Number = id.ToString("000000", System.Globalization.CultureInfo.InvariantCulture),
            StatusCode = status,
            LanguageCode = language,
            Title = title ?? $"Initiative {id} ({language})",
            TotalSupporters = supporters,
            SupportLink = new Uri($"https://eci.ec.europa.eu/{id:000}/public/?lg={language.ToLowerInvariant()}"),
            LatestUpdateDate = updated ?? new DateTime(2026, 11, 1, 10, 0, 0),
            LastCall = lastCall,
        };

    public static InitiativeDetails Details(InitiativeSummary summary, DateOnly? deadline = null) => new()
    {
        Id = summary.Id,
        ComRegNum = summary.PubRegNum,
        StatusCode = summary.StatusCode,
        LatestUpdateDate = summary.LatestUpdateDate,
        RegistrationDate = new DateOnly(2026, 1, 1).AddDays((int)(summary.Id % 300)),
        StartCollectionDate = new DateOnly(2026, 3, 1),
        Deadline = deadline ?? new DateOnly(2027, 3, 1),
        LinguisticVersions =
        [
            new LinguisticVersion { Original = true, LanguageCode = "EN", Title = $"Initiative {summary.Id} (EN)" },
            new LinguisticVersion { LanguageCode = "EL", Title = $"Πρωτοβουλία {summary.Id}", SupportLink = new Uri($"https://eci.ec.europa.eu/{summary.Id:000}/public/?lg=el") },
        ],
    };
}

/// <summary>In-memory register whose content the tests change between syncs.</summary>
internal sealed class FakeRegister : IRegisterApiClient
{
    public List<InitiativeSummary> Summaries { get; } = [];

    public Dictionary<long, Func<InitiativeDetails>> Details { get; } = [];

    public HashSet<long> BrokenDetails { get; } = [];

    public bool ListFails { get; set; }

    public List<long> DetailRequests { get; } = [];

    /// <summary>When set, details answer asynchronously after this delay, so downloads really overlap.</summary>
    public TimeSpan DetailDelay { get; set; }

    /// <summary>Most detail requests that were running at the same time.</summary>
    public int MaxConcurrentDetails { get; private set; }

    private int _runningDetails;

    public void Set(params InitiativeSummary[] summaries)
    {
        Summaries.Clear();
        Summaries.AddRange(summaries);
        foreach (var s in summaries)
        {
            Details[s.Id] = () => Make.Details(Summaries.First(x => x.Id == s.Id));
        }
    }

    public void Replace(InitiativeSummary summary) =>
        Summaries[Summaries.FindIndex(s => s.Id == summary.Id)] = summary;

    public Task<IReadOnlyList<InitiativeSummary>> GetAllAsync(string language, CancellationToken cancellationToken = default) =>
        ListFails
            ? throw new HttpRequestException("offline")
            : Task.FromResult<IReadOnlyList<InitiativeSummary>>([.. Summaries]);

    public Task<InitiativeDetails> GetDetailsAsync(string year, string number, CancellationToken cancellationToken = default)
    {
        var id = Summaries.First(s => s.Year == year && s.Number == number).Id;
        lock (DetailRequests)
        {
            DetailRequests.Add(id);
        }

        if (DetailDelay > TimeSpan.Zero)
        {
            return DelayedDetailsAsync(id, cancellationToken);
        }

        return BrokenDetails.Contains(id)
            ? throw new EciApiException(HttpStatusCode.InternalServerError, null)
            : Task.FromResult(Details[id]());
    }

    private async Task<InitiativeDetails> DelayedDetailsAsync(long id, CancellationToken cancellationToken)
    {
        var running = Interlocked.Increment(ref _runningDetails);
        lock (DetailRequests)
        {
            MaxConcurrentDetails = Math.Max(MaxConcurrentDetails, running);
        }

        try
        {
            await Task.Delay(DetailDelay, cancellationToken);
            return BrokenDetails.Contains(id)
                ? throw new EciApiException(HttpStatusCode.InternalServerError, null)
                : Details[id]();
        }
        finally
        {
            Interlocked.Decrement(ref _runningDetails);
        }
    }

    public Task<RegisterStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new RegisterStatistics { Registered = Summaries.Count, Ongoing = Summaries.Count(s => s.Status == EciStatus.Ongoing) });

    public Task<RegisterSearchResult> SearchAsync(RegisterSearchFilter filter, string language, int offset, int pageSize, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<byte[]> GetLogoAsync(long logoId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class FakeNews : INewsFeedClient
{
    public List<NewsItem> Items { get; } = [];

    public bool Fails { get; set; }

    public Task<IReadOnlyList<NewsItem>> GetLatestAsync(string language, CancellationToken cancellationToken = default) =>
        Fails
            ? throw new EciApiException(HttpStatusCode.TooManyRequests, null)
            : Task.FromResult<IReadOnlyList<NewsItem>>([.. Items]);

    public static NewsItem Item(int n, string language = "en") => new()
    {
        Id = $"https://citizens-initiative.europa.eu/node/{n}",
        Title = $"News {n}",
        Language = language,
        Published = new DateTimeOffset(2026, 11, n, 0, 0, 0, TimeSpan.FromHours(1)),
        Link = new Uri($"https://citizens-initiative.europa.eu/news/{n}_{language}"),
    };
}
