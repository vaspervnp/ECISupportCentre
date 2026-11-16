using System.Globalization;
using EciSupportCentre.Core.Localization;

namespace EciSupportCentre.Core.Tests;

public class LanguageChoiceTests
{
    [Theory]
    // Follow the device: keep its regional formats when the language is supported.
    [InlineData(null, "el-GR", "el-GR")]
    [InlineData("", "de-AT", "de-AT")]
    [InlineData(null, "en-GB", "en-GB")]
    [InlineData(null, "fr", "fr")]
    [InlineData(null, "pt_BR", "pt-BR")]
    // A device language outside the EU: English with EU formats.
    [InlineData(null, "ja-JP", "en-IE")]
    [InlineData(null, "", "en-IE")]
    [InlineData(null, null, "en-IE")]
    // An explicit choice: the device culture if it is the same language, else the language's default culture.
    [InlineData("el", "en-US", "el-GR")]
    [InlineData("de", "de-CH", "de-CH")]
    [InlineData("GA", "en-IE", "ga-IE")]
    [InlineData("en", "el-GR", "en-IE")]
    // Not an EU language: ignored.
    [InlineData("ja", "el-GR", "el-GR")]
    public void Culture_follows_the_choice_then_the_device_then_English(string? preference, string? device, string expected) =>
        Assert.Equal(expected, LanguageChoice.Resolve(preference, device));

    [Fact]
    public void All_24_EU_languages_are_offered_once_with_a_culture_dotnet_knows()
    {
        Assert.Equal(EciSources.Languages.Order(), LanguageChoice.All.Select(l => l.Code).Order());
        Assert.All(LanguageChoice.All, l =>
        {
            var culture = CultureInfo.GetCultureInfo(l.DefaultCulture);
            Assert.Equal(l.Code, culture.TwoLetterISOLanguageName);
            Assert.False(string.IsNullOrWhiteSpace(l.NativeName));
        });
    }
}
