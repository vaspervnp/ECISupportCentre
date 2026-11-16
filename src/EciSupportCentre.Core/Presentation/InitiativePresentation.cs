using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Storage;

namespace EciSupportCentre.Core.Presentation;

public enum DateLineKind
{
    None,
    StartsOn,
    EndsOn,
    EndedOn,
}

/// <summary>UI-independent rules for showing initiatives. The app adds the translated texts.</summary>
public static class InitiativePresentation
{
    /// <summary>The one date a list card shows: when collection starts, ends or ended.</summary>
    public static (DateLineKind Kind, DateOnly? Date) DateLine(InitiativeListItem item, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Phase switch
        {
            InitiativePhase.Upcoming when item.StartCollectionDate is { } start && start > today => (DateLineKind.StartsOn, start),
            InitiativePhase.Collecting when item.CollectionEndDate is { } end => (DateLineKind.EndsOn, end),
            InitiativePhase.Verification or InitiativePhase.Submitted or InitiativePhase.Answered or InitiativePhase.Failed
                when item.CollectionEndDate is { } ended => (DateLineKind.EndedOn, ended),
            _ => (DateLineKind.None, null),
        };
    }

    /// <summary>Timeline steps in date order; steps without a date go last.</summary>
    public static IReadOnlyList<ProgressStep> Timeline(InitiativeDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        return
        [
            .. details.Progress
                .Where(p => !string.IsNullOrEmpty(p.Step))
                .OrderBy(p => p.Date is null)
                .ThenBy(p => p.Date)
                .ThenBy(p => StepOrder(p.Step)),
        ];
    }

    /// <summary>Countries with the most signatures (verified if available).</summary>
    public static IReadOnlyList<CountrySignatures> TopCountries(InitiativeDetails details, int count)
    {
        ArgumentNullException.ThrowIfNull(details);

        var report = details.Submission?.Countries.Count > 0 ? details.Submission : details.SosReport;
        return [.. (report?.Countries ?? []).Where(c => c.Total > 0).OrderByDescending(c => c.Total).Take(count)];
    }

    /// <summary>
    /// ISO 3166 code for a country code used by the EU. The EU writes Greece as <c>EL</c> and the United Kingdom as <c>UK</c>.
    /// </summary>
    public static string ToIsoCountry(string euCode)
    {
        ArgumentNullException.ThrowIfNull(euCode);

        return euCode.Trim().ToUpperInvariant() switch
        {
            "EL" => "GR",
            "UK" => "GB",
            var code => code,
        };
    }

    /// <summary>Best link of an answer document for the language, else its default link.</summary>
    public static Uri? DocumentLink(AnswerLink link, string language)
    {
        ArgumentNullException.ThrowIfNull(link);

        var code = EciSources.Normalize(language);
        return link.Translations
                   .FirstOrDefault(t => string.Equals(t.LanguageCode, code, StringComparison.OrdinalIgnoreCase))?.Link
               ?? link.DefaultLink;
    }

    private static int StepOrder(string step) => step switch
    {
        "REGISTERED" => 0,
        "COLLECTION_START_DATE" => 1,
        "ONGOING" => 2,
        "CLOSED" => 3,
        "VERIFICATION" => 4,
        "SUBMITTED" => 5,
        "ANSWERED" => 6,
        _ => 7,
    };
}
