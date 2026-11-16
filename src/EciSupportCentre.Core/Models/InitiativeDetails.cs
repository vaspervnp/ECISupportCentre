using System.Text.Json.Serialization;
using EciSupportCentre.Core.Serialization;

namespace EciSupportCentre.Core.Models;

/// <summary>Response of <c>register/details/{year}/{number}</c>. Holds every language version.</summary>
public sealed record InitiativeDetails
{
    public long Id { get; init; }

    /// <summary>Commission registration number, e.g. <c>ECI(2026)000009</c>.</summary>
    public string ComRegNum { get => field ?? ""; init; }

    [JsonPropertyName("status")]
    public string StatusCode { get => field ?? ""; init; }

    [JsonIgnore]
    public EciStatus Status => EciStatusExtensions.ParseStatus(StatusCode);

    [JsonConverter(typeof(EciDateTimeConverter))]
    public DateTime? LatestUpdateDate { get; init; }

    public bool LastCall { get; init; }

    [JsonConverter(typeof(EciDateConverter))]
    public DateOnly? RegistrationDate { get; init; }

    [JsonConverter(typeof(EciDateConverter))]
    public DateOnly? StartCollectionDate { get; init; }

    /// <summary>End of the collection period, while the initiative is collecting.</summary>
    [JsonConverter(typeof(EciDateConverter))]
    public DateOnly? Deadline { get; init; }

    /// <summary>Set when the organisers closed the collection before the deadline.</summary>
    [JsonConverter(typeof(EciDateConverter))]
    public DateOnly? EarlyClosureDate { get; init; }

    public bool PartiallyRegistered { get; init; }

    public IReadOnlyList<LinguisticVersion> LinguisticVersions { get => field ?? []; init; }

    public IReadOnlyList<EciCategory> Categories { get => field ?? []; init; }

    public IReadOnlyList<EciMember> Members { get => field ?? []; init; }

    public IReadOnlyList<ProgressStep> Progress { get => field ?? []; init; }

    /// <summary>Signatures collected so far (statements of support).</summary>
    public SignatureReport? SosReport { get; init; }

    /// <summary>Verified signatures, after the collection closed.</summary>
    public SignatureReport? Submission { get; init; }

    public CommissionAnswer? Answer { get; init; }

    /// <summary>Links published while the Commission examines a submitted initiative.</summary>
    public CommissionAnswer? PreAnswer { get; init; }

    public EciFunding? Funding { get; init; }

    public EciFile? Logo { get; init; }

    [JsonIgnore]
    public InitiativePhase Phase => Status.ToPhase();

    [JsonIgnore]
    public InitiativeOutcome Outcome => Status.ToOutcome();

    /// <summary>The language version the organisers registered.</summary>
    [JsonIgnore]
    public LinguisticVersion? OriginalVersion =>
        LinguisticVersions.FirstOrDefault(v => v.Original) ?? (LinguisticVersions.Count > 0 ? LinguisticVersions[0] : null);

    /// <summary>Best number of signatures known: verified if available, else collected.</summary>
    [JsonIgnore]
    public long? TotalSignatures => Submission?.TotalSignatures ?? SosReport?.TotalSignatures;

    /// <summary>The day the collection ended or will end, if known.</summary>
    [JsonIgnore]
    public DateOnly? CollectionEndDate =>
        EarlyClosureDate
        ?? Deadline
        ?? Progress.FirstOrDefault(p => p.Step == "CLOSED")?.Date;

    /// <summary>Returns the version in <paramref name="language"/>, else English, else the original.</summary>
    public LinguisticVersion? GetVersion(string? language)
    {
        var code = EciSources.Normalize(language);
        return Find(code) ?? Find(EciSources.DefaultLanguage) ?? OriginalVersion;

        LinguisticVersion? Find(string c) =>
            LinguisticVersions.FirstOrDefault(v => string.Equals(v.LanguageCode, c, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record LinguisticVersion
{
    public bool Original { get; init; }

    public string LanguageCode { get => field ?? ""; init; }

    public string Title { get => field ?? ""; init; }

    /// <summary>Objectives as HTML.</summary>
    public string? Objectives { get; init; }

    /// <summary>Additional information (annex) as HTML.</summary>
    public string? AnnexText { get; init; }

    public string? Treaties { get; init; }

    public Uri? Website { get; init; }

    public Uri? SupportLink { get; init; }

    public EciFile? DraftLegal { get; init; }

    public CommissionDecision? CommissionDecision { get; init; }
}

public sealed record CommissionDecision
{
    public string? Celex { get; init; }

    public Uri? Url { get; init; }
}

public sealed record EciCategory
{
    /// <summary>Policy area code, e.g. <c>EDU</c>, <c>SANTE</c>.</summary>
    public string CategoryType { get => field ?? ""; init; }
}

public sealed record EciMember
{
    /// <summary><c>REPRESENTATIVE</c>, <c>SUBSTITUTE</c>, <c>MEMBER</c>, <c>DPO</c>, ...</summary>
    public string Type { get => field ?? ""; init; }

    public string? FullName { get; init; }

    public string? ResidenceCountry { get; init; }

    public bool PrivacyApplied { get; init; }
}

public sealed record ProgressStep
{
    /// <summary><c>REGISTERED</c>, <c>COLLECTION_START_DATE</c>, <c>ONGOING</c>, <c>CLOSED</c>, <c>VERIFICATION</c>, <c>SUBMITTED</c>, <c>ANSWERED</c>, ...</summary>
    [JsonPropertyName("name")]
    public string Step { get => field ?? ""; init; }

    /// <summary>True for the current step.</summary>
    public bool Active { get; init; }

    [JsonConverter(typeof(EciDateConverter))]
    public DateOnly? Date { get; init; }

    /// <summary>Extra information, e.g. <c>COLLECTION_EARLY_CLOSURE</c>.</summary>
    public string? FootnoteType { get; init; }
}

public sealed record SignatureReport
{
    public long TotalSignatures { get; init; }

    [JsonConverter(typeof(EciDateTimeConverter))]
    public DateTime? OnlineSosUpdateDate { get; init; }

    [JsonConverter(typeof(EciDateTimeConverter))]
    public DateTime? UpdateDate { get; init; }

    /// <summary>Signatures per member state.</summary>
    [JsonPropertyName("entry")]
    public IReadOnlyList<CountrySignatures> Countries { get => field ?? []; init; }
}

public sealed record CountrySignatures
{
    /// <summary>Country code as used by the EU, e.g. <c>EL</c> for Greece.</summary>
    [JsonPropertyName("countryCodeType")]
    public string CountryCode { get => field ?? ""; init; }

    public long Total { get; init; }

    public bool AfterSubmission { get; init; }
}

public sealed record CommissionAnswer
{
    public long? Id { get; init; }

    [JsonConverter(typeof(EciDateConverter))]
    public DateOnly? DecisionDate { get; init; }

    public IReadOnlyList<AnswerLink> Links { get => field ?? []; init; }
}

public sealed record AnswerLink
{
    public string? DefaultLanguageCode { get; init; }

    /// <summary>Kind of document, e.g. <c>COMMUNICATION</c>, <c>EXAMIN_STEPS</c>.</summary>
    public string? DefaultName { get; init; }

    public Uri? DefaultLink { get; init; }

    [JsonPropertyName("link")]
    public IReadOnlyList<LocalizedLink> Translations { get => field ?? []; init; }
}

public sealed record LocalizedLink
{
    public string LanguageCode { get => field ?? ""; init; }

    public Uri? Link { get; init; }
}

public sealed record EciFunding
{
    [JsonConverter(typeof(EciDateConverter))]
    public DateOnly? LastUpdate { get; init; }

    public decimal TotalAmount { get; init; }
}
