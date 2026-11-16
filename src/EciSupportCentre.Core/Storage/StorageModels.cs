using EciSupportCentre.Core.Models;

namespace EciSupportCentre.Core.Storage;

/// <summary>What the sync engine needs to know about a stored initiative to detect changes.</summary>
public sealed record InitiativeSnapshot
{
    public required long Id { get; init; }

    public required string Year { get; init; }

    public required string Number { get; init; }

    public required EciStatus Status { get; init; }

    public long TotalSupporters { get; init; }

    public bool LastCall { get; init; }

    public DateTime? LatestUpdateDate { get; init; }

    /// <summary><c>latestUpdateDate</c> of the stored details; null when details were never fetched.</summary>
    public DateTime? DetailsLatestUpdate { get; init; }

    public bool HasDetails { get; init; }
}

public enum ChangeKind
{
    /// <summary>An initiative appeared in the register.</summary>
    NewInitiative,

    /// <summary>The status changed; old and new values are status codes.</summary>
    StatusChanged,

    /// <summary>Supporters crossed a milestone; the new value is the milestone.</summary>
    SignatureMilestone,

    /// <summary>The register flagged the initiative's last weeks of collection.</summary>
    LastCall,
}

/// <summary>A change found during a sync, kept until the app has notified the user.</summary>
public sealed record InitiativeChange
{
    /// <summary>Database id; 0 before it is stored.</summary>
    public long Id { get; init; }

    public required long InitiativeId { get; init; }

    public required ChangeKind Kind { get; init; }

    public string? OldValue { get; init; }

    public string? NewValue { get; init; }

    public required DateTimeOffset DetectedAt { get; init; }

    public bool Notified { get; init; }

    /// <summary>For <see cref="ChangeKind.StatusChanged"/>: the new status.</summary>
    public EciStatus NewStatus => EciStatusExtensions.ParseStatus(NewValue);
}

/// <summary>An initiative as the list and detail screens show it, in one language.</summary>
public sealed record InitiativeListItem
{
    public required long Id { get; init; }

    public required string PubRegNum { get; init; }

    public required string Year { get; init; }

    public required string Number { get; init; }

    public required EciStatus Status { get; init; }

    public InitiativePhase Phase => Status.ToPhase();

    public InitiativeOutcome Outcome => Status.ToOutcome();

    public required string Title { get; init; }

    /// <summary>Language of <see cref="Title"/>; differs from the requested one when a fallback was used.</summary>
    public required string TitleLanguage { get; init; }

    public long TotalSupporters { get; init; }

    /// <summary>Signing page in the requested language, when the initiative collects online.</summary>
    public Uri? SupportLink { get; init; }

    public bool LastCall { get; init; }

    public long? LogoId { get; init; }

    public DateOnly? RegistrationDate { get; init; }

    public DateOnly? StartCollectionDate { get; init; }

    /// <summary>End of collection; known once details have been fetched.</summary>
    public DateOnly? CollectionEndDate { get; init; }

    public DateTime? LatestUpdateDate { get; init; }

    public bool HasDetails { get; init; }

    public bool IsFollowed { get; init; }

    public bool CanBeSigned => Status.CanBeSigned() && SupportLink is not null;
}
