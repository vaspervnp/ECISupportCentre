using EciSupportCentre.Core;

namespace EciSupportCentre.Core.Tests;

public class EciSourcesTests
{
    [Fact]
    public void Languages_contains_all_24_official_languages()
    {
        Assert.Equal(24, EciSources.Languages.Count);
        Assert.Equal(24, EciSources.Languages.Distinct().Count());
    }

    [Theory]
    [InlineData("el-GR", "el")]
    [InlineData("DE", "de")]
    [InlineData("pt_BR", "pt")]
    [InlineData("ja-JP", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void Normalize_maps_cultures_to_eu_languages(string? input, string expected) =>
        Assert.Equal(expected, EciSources.Normalize(input));

    [Fact]
    public void Website_uses_language_suffix() =>
        Assert.Equal("https://citizens-initiative.europa.eu/index_fr", EciSources.Website("fr-BE").ToString());
}
