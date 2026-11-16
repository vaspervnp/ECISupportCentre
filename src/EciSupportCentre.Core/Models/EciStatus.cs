namespace EciSupportCentre.Core.Models;

/// <summary>Status of an initiative as published by the register.</summary>
public enum EciStatus
{
    Unknown,
    Registered,
    Ongoing,
    Closed,
    Verification,
    Submitted,
    Answered,
    InsufficientSupport,
    InsufficientSupportAfterVerification,
    Withdrawn,
}

/// <summary>Simplified stage of an initiative, used by the UI and notifications.</summary>
public enum InitiativePhase
{
    Unknown,

    /// <summary>Registered; collection has not started.</summary>
    Upcoming,

    /// <summary>Collecting signatures.</summary>
    Collecting,

    /// <summary>Collection closed; signatures are being verified.</summary>
    Verification,

    /// <summary>Submitted to the Commission with at least one million valid signatures.</summary>
    Submitted,

    /// <summary>The Commission has answered.</summary>
    Answered,

    /// <summary>Not enough signatures, before or after verification.</summary>
    Failed,

    /// <summary>Withdrawn by the organisers.</summary>
    Withdrawn,
}

/// <summary>Final result of an initiative.</summary>
public enum InitiativeOutcome
{
    /// <summary>Not decided yet.</summary>
    Pending,

    /// <summary>Reached the thresholds and was submitted to the Commission.</summary>
    Successful,

    Failed,
    Withdrawn,
}

public static class EciStatusExtensions
{
    /// <summary>Parses a register status code such as <c>INSUFFICIENT_SUPPORT</c>. Unknown codes give <see cref="EciStatus.Unknown"/>.</summary>
    public static EciStatus ParseStatus(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "REGISTERED" => EciStatus.Registered,
        "ONGOING" => EciStatus.Ongoing,
        "CLOSED" => EciStatus.Closed,
        "VERIFICATION" => EciStatus.Verification,
        "SUBMITTED" => EciStatus.Submitted,
        "ANSWERED" => EciStatus.Answered,
        "INSUFFICIENT_SUPPORT" => EciStatus.InsufficientSupport,
        "INSUFFICIENT_SUPPORT_AFTER_VERIFICATION" => EciStatus.InsufficientSupportAfterVerification,
        "WITHDRAWN" => EciStatus.Withdrawn,
        _ => EciStatus.Unknown,
    };

    public static InitiativePhase ToPhase(this EciStatus status) => status switch
    {
        EciStatus.Registered => InitiativePhase.Upcoming,
        EciStatus.Ongoing => InitiativePhase.Collecting,
        EciStatus.Closed or EciStatus.Verification => InitiativePhase.Verification,
        EciStatus.Submitted => InitiativePhase.Submitted,
        EciStatus.Answered => InitiativePhase.Answered,
        EciStatus.InsufficientSupport or EciStatus.InsufficientSupportAfterVerification => InitiativePhase.Failed,
        EciStatus.Withdrawn => InitiativePhase.Withdrawn,
        _ => InitiativePhase.Unknown,
    };

    public static InitiativeOutcome ToOutcome(this EciStatus status) => status switch
    {
        EciStatus.Submitted or EciStatus.Answered => InitiativeOutcome.Successful,
        EciStatus.InsufficientSupport or EciStatus.InsufficientSupportAfterVerification => InitiativeOutcome.Failed,
        EciStatus.Withdrawn => InitiativeOutcome.Withdrawn,
        _ => InitiativeOutcome.Pending,
    };

    /// <summary>True while people can sign the initiative.</summary>
    public static bool CanBeSigned(this EciStatus status) => status == EciStatus.Ongoing;
}
