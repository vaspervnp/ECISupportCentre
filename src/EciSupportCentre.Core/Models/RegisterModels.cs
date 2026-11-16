using System.Text.Json.Serialization;
using EciSupportCentre.Core.Serialization;

namespace EciSupportCentre.Core.Models;

/// <summary>Response of <c>register/search/{filter}/{lang}/{offset}/{size}</c>.</summary>
public sealed record RegisterSearchResult
{
    /// <summary>Registration requests received, including refused ones.</summary>
    public int Requests { get; init; }

    public int Registered { get; init; }

    /// <summary>Initiatives that reached the thresholds (submitted or answered).</summary>
    public int Successful { get; init; }

    public int Ongoing { get; init; }

    public int Answered { get; init; }

    public int All { get; init; }

    /// <summary>Entries matching the filter, across all pages.</summary>
    public int RecordsFound { get; init; }

    public IReadOnlyList<InitiativeSummary> Entries { get => field ?? []; init; }
}

/// <summary>One initiative in a search result, in the requested language.</summary>
public sealed record InitiativeSummary
{
    public long Id { get; init; }

    /// <summary>Public registration number, e.g. <c>ECI(2026)000009</c>.</summary>
    public string PubRegNum { get => field ?? ""; init; }

    public string Year { get => field ?? ""; init; }

    public string Number { get => field ?? ""; init; }

    [JsonPropertyName("status")]
    public string StatusCode { get => field ?? ""; init; }

    [JsonIgnore]
    public EciStatus Status => EciStatusExtensions.ParseStatus(StatusCode);

    public string LanguageCode { get => field ?? ""; init; }

    public string Title { get => field ?? ""; init; }

    public long TotalSupporters { get; init; }

    /// <summary>Official online collection page. Present only for initiatives that collected online.</summary>
    public Uri? SupportLink { get; init; }

    [JsonConverter(typeof(EciDateTimeConverter))]
    public DateTime? LatestUpdateDate { get; init; }

    /// <summary>The register flags initiatives in their last weeks of collection.</summary>
    public bool LastCall { get; init; }

    public EciFile? Logo { get; init; }
}

/// <summary>A file stored in the register (logo, draft legal act, ...).</summary>
public sealed record EciFile
{
    public long Id { get; init; }

    public string? Name { get; init; }

    public string? MimeType { get; init; }

    public long? Size { get; init; }

    public string? Language { get; init; }
}

/// <summary>Response of <c>register/statistics</c>.</summary>
public sealed record RegisterStatistics
{
    public int Registered { get; init; }

    public int Ongoing { get; init; }

    public int Answered { get; init; }

    /// <summary>Registration requests the Commission refused.</summary>
    public int Rejected { get; init; }

    public int Unsuccessful { get; init; }

    public int UnsuccessfulAfterVerification { get; init; }

    public int Closed { get; init; }

    public int Withdrawn { get; init; }

    public int Submitted { get; init; }

    public int Verification { get; init; }

    public int StartingSoon { get; init; }
}
