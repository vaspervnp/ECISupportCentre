using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Serialization;

namespace EciSupportCentre.Core.Api;

/// <summary>Filters the register's search endpoint accepts. Other statuses are filtered on the device.</summary>
public enum RegisterSearchFilter
{
    All,
    Ongoing,
    Answered,
}

public interface IRegisterApiClient
{
    /// <summary>One page of initiatives, titles in <paramref name="language"/>.</summary>
    /// <remarks>
    /// <paramref name="offset"/> is an index, not a page number. The server applies offset and size for
    /// <see cref="RegisterSearchFilter.All"/> and <see cref="RegisterSearchFilter.Answered"/>, but returns every
    /// entry for <see cref="RegisterSearchFilter.Ongoing"/>.
    /// </remarks>
    Task<RegisterSearchResult> SearchAsync(RegisterSearchFilter filter, string language, int offset, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Every initiative of the register, following all pages.</summary>
    Task<IReadOnlyList<InitiativeSummary>> GetAllAsync(string language, CancellationToken cancellationToken = default);

    /// <summary>Full record of one initiative, with all its language versions.</summary>
    Task<InitiativeDetails> GetDetailsAsync(string year, string number, CancellationToken cancellationToken = default);

    Task<RegisterStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>Logo image bytes (the register serves them as <c>application/octet-stream</c>).</summary>
    Task<byte[]> GetLogoAsync(long logoId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Client for the public ECI register API at <see cref="EciSources.RegisterApiBase"/>.
/// </summary>
public sealed class RegisterApiClient(HttpClient httpClient, Uri? baseAddress = null) : IRegisterApiClient
{
    /// <summary>Largest page the client asks for. The server accepts more, but smaller pages fail less on mobile networks.</summary>
    public const int MaxPageSize = 100;

    private readonly Uri _base = baseAddress ?? EciSources.RegisterApiBase;

    public Task<RegisterSearchResult> SearchAsync(RegisterSearchFilter filter, string language, int offset, int pageSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"search/{FilterCode(filter)}/{LanguageCode(language)}/{offset}/{Math.Min(pageSize, MaxPageSize)}");
        return GetJsonAsync(path, EciJsonContext.Default.RegisterSearchResult, cancellationToken);
    }

    public async Task<IReadOnlyList<InitiativeSummary>> GetAllAsync(string language, CancellationToken cancellationToken = default)
    {
        var all = new List<InitiativeSummary>();
        var seen = new HashSet<long>();
        var offset = 0;

        while (true)
        {
            var page = await SearchAsync(RegisterSearchFilter.All, language, offset, MaxPageSize, cancellationToken).ConfigureAwait(false);
            foreach (var entry in page.Entries)
            {
                // The register can change between pages; never return an initiative twice.
                if (seen.Add(entry.Id))
                {
                    all.Add(entry);
                }
            }

            offset += page.Entries.Count;
            if (page.Entries.Count == 0 || offset >= page.RecordsFound)
            {
                return all;
            }
        }
    }

    public Task<InitiativeDetails> GetDetailsAsync(string year, string number, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(year);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);

        var path = $"details/{Uri.EscapeDataString(year.Trim())}/{Uri.EscapeDataString(number.Trim())}";
        return GetJsonAsync(path, EciJsonContext.Default.InitiativeDetails, cancellationToken);
    }

    public Task<RegisterStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync("statistics", EciJsonContext.Default.RegisterStatistics, cancellationToken);

    public async Task<byte[]> GetLogoAsync(long logoId, CancellationToken cancellationToken = default)
    {
        var uri = new Uri(_base, string.Create(CultureInfo.InvariantCulture, $"logo/{logoId}"));
        using var response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, uri);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> GetJsonAsync<T>(string relativePath, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        var uri = new Uri(_base, relativePath);
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, uri);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            try
            {
                return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false)
                    ?? throw new EciApiException($"Empty response from {uri}.");
            }
            catch (JsonException ex)
            {
                throw new EciApiException($"Unexpected response from {uri}: {ex.Message}", ex);
            }
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, Uri uri)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new EciApiException(response.StatusCode, uri);
        }
    }

    internal static string FilterCode(RegisterSearchFilter filter) => filter switch
    {
        RegisterSearchFilter.All => "ALL",
        RegisterSearchFilter.Ongoing => "ONGOING",
        RegisterSearchFilter.Answered => "ANSWERED",
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
    };

    internal static string LanguageCode(string? language) => EciSources.Normalize(language).ToUpperInvariant();
}
