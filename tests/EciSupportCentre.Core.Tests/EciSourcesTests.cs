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

    [Theory]
    [InlineData("https://eci.ec.europa.eu/069/public/?lg=en", "el-GR", "https://eci.ec.europa.eu/069/public/?lg=el")]
    [InlineData("https://eci.ec.europa.eu/069/public/", "de", "https://eci.ec.europa.eu/069/public/?lg=de")]
    [InlineData("https://eci.ec.europa.eu/069/public/?x=1&LG=fr", "ja", "https://eci.ec.europa.eu/069/public/?x=1&lg=en")]
    [InlineData("https://ECI.EC.EUROPA.EU/045/public/#/screen/home?lg=en", "pt", "https://eci.ec.europa.eu/045/public/?lg=pt#/screen/home?lg=en")]
    public void Signing_page_uses_the_language(string link, string language, string expected) =>
        Assert.Equal(new Uri(expected), EciSources.SigningPage(new Uri(link), language));

    [Theory]
    [InlineData("http://eci.ec.europa.eu/069/public/?lg=en")]
    [InlineData("https://eci.ec.europa.eu.evil.example/069/public/")]
    [InlineData("https://example.org/sign")]
    [InlineData(null)]
    public void Signing_page_accepts_only_the_official_collection_system(string? link) =>
        Assert.Null(EciSources.SigningPage(link is null ? null : new Uri(link), "en"));

    [Fact]
    public void Website_uses_language_suffix() =>
        Assert.Equal("https://citizens-initiative.europa.eu/index_fr", EciSources.Website("fr-BE").ToString());

    [Theory]
    [InlineData("el", "https://citizens-initiative.europa.eu/find-initiative_el", "https://citizens-initiative.europa.eu/news_el")]
    [InlineData("ja", "https://citizens-initiative.europa.eu/find-initiative_en", "https://citizens-initiative.europa.eu/news_en")]
    public void Source_pages_use_language_suffix(string language, string initiatives, string news)
    {
        Assert.Equal(initiatives, EciSources.InitiativesPage(language).ToString());
        Assert.Equal(news, EciSources.NewsPage(language).ToString());
    }
}
