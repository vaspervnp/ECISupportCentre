using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Storage;

namespace EciSupportCentre.Data.Tests;

public class InitiativeQueryTests
{
    private static InitiativeListItem Item(long id, string status, string title, long supporters = 0, DateOnly? end = null, DateOnly? registered = null) => new()
    {
        Id = id,
        PubRegNum = $"ECI(2026){id:000000}",
        Year = "2026",
        Number = $"{id:000000}",
        Status = EciStatusExtensions.ParseStatus(status),
        Title = title,
        TitleLanguage = "el",
        TotalSupporters = supporters,
        CollectionEndDate = end,
        RegistrationDate = registered,
    };

    private static readonly InitiativeListItem[] Items =
    [
        Item(1, "ONGOING", "Δικαίωμα στη στέγαση", 26_000, new DateOnly(2027, 8, 1), new DateOnly(2026, 6, 1)),
        Item(2, "ONGOING", "Κάθε άνθρωπος δεκτός", 134, new DateOnly(2027, 11, 13), new DateOnly(2026, 7, 17)),
        Item(3, "ANSWERED", "Stop Destroying Videogames", 1_294_188, new DateOnly(2025, 7, 31), new DateOnly(2024, 6, 19)),
        Item(4, "INSUFFICIENT_SUPPORT", "Save your flight", 5_000, new DateOnly(2026, 8, 8), new DateOnly(2025, 7, 16)),
    ];

    [Fact]
    public void Default_sort_is_newest_registration_first() =>
        Assert.Equal([2, 1, 4, 3], new InitiativeQuery().Apply(Items).Select(i => i.Id));

    [Fact]
    public void Search_ignores_case_and_accents() =>
        Assert.Equal([2], new InitiativeQuery { Search = "καθε ΑΝΘΡΩΠΟΣ" }.Apply(Items).Select(i => i.Id));

    [Fact]
    public void Search_matches_the_registration_number() =>
        Assert.Equal([3], new InitiativeQuery { Search = "(2026)000003" }.Apply(Items).Select(i => i.Id));

    [Fact]
    public void Filters_by_phase() =>
        Assert.Equal(
            [3, 4],
            new InitiativeQuery { Phases = new HashSet<InitiativePhase> { InitiativePhase.Answered, InitiativePhase.Failed }, Sort = InitiativeSort.MostSupporters }
                .Apply(Items).Select(i => i.Id));

    [Fact]
    public void Sorts_by_supporters() =>
        Assert.Equal([3, 1, 4, 2], new InitiativeQuery { Sort = InitiativeSort.MostSupporters }.Apply(Items).Select(i => i.Id));

    [Fact]
    public void Ending_soon_puts_collecting_initiatives_first() =>
        Assert.Equal([1, 2, 3, 4], new InitiativeQuery { Sort = InitiativeSort.EndingSoon }.Apply(Items).Select(i => i.Id));
}
