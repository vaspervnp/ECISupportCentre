using System.Globalization;
using EciSupportCentre.Core.Models;

namespace EciSupportCentre.Core.Storage;

public enum InitiativeSort
{
    /// <summary>Most recently registered first.</summary>
    Newest,

    MostSupporters,

    /// <summary>Collecting initiatives whose collection ends first; others after.</summary>
    EndingSoon,

    RecentlyUpdated,
}

/// <summary>Filter and sort for the initiative list. The register is small (a few hundred rows), so this runs in memory.</summary>
public sealed record InitiativeQuery
{
    public string Language { get; init; } = EciSources.DefaultLanguage;

    /// <summary>Phases to show; empty means all.</summary>
    public IReadOnlySet<InitiativePhase> Phases { get; init; } = new HashSet<InitiativePhase>();

    /// <summary>Free text; matches title or registration number, ignoring case and accents.</summary>
    public string? Search { get; init; }

    public bool FollowedOnly { get; init; }

    public InitiativeSort Sort { get; init; } = InitiativeSort.Newest;

    public IReadOnlyList<InitiativeListItem> Apply(IEnumerable<InitiativeListItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var search = Search?.Trim();
        var compare = CultureInfo.InvariantCulture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

        var filtered = items.Where(i =>
            (Phases.Count == 0 || Phases.Contains(i.Phase))
            && (!FollowedOnly || i.IsFollowed)
            && (string.IsNullOrEmpty(search)
                || compare.IndexOf(i.Title, search, options) >= 0
                || compare.IndexOf(i.PubRegNum, search, options) >= 0));

        var sorted = Sort switch
        {
            InitiativeSort.MostSupporters => filtered.OrderByDescending(i => i.TotalSupporters),
            InitiativeSort.EndingSoon => filtered
                .OrderBy(i => i.Phase == InitiativePhase.Collecting ? 0 : 1)
                .ThenBy(i => i.CollectionEndDate ?? DateOnly.MaxValue),
            InitiativeSort.RecentlyUpdated => filtered.OrderByDescending(i => i.LatestUpdateDate ?? DateTime.MinValue),
            _ => filtered
                .OrderByDescending(i => i.RegistrationDate ?? DateOnly.MinValue)
                .ThenByDescending(i => i.PubRegNum, StringComparer.Ordinal),
        };

        return [.. sorted.ThenBy(i => i.Id)];
    }
}
