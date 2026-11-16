using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace EciSupportCentre.Core.Tests;

/// <summary>Checks the app's translated .resx files against the English one.</summary>
public partial class TranslationTests
{
    private static readonly string StringsFolder = Path.Combine(RepositoryRoot(), "src", "EciSupportCentre.App", "Resources", "Strings");

    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (var code in EciSources.Languages.Where(l => l != EciSources.DefaultLanguage))
        {
            data.Add(code);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_language_has_every_text_with_the_same_placeholders(string language)
    {
        var english = Read("AppResources.resx");
        var path = $"AppResources.{language}.resx";
        Assert.True(File.Exists(Path.Combine(StringsFolder, path)), $"{path} is missing");
        var translated = Read(path);

        Assert.Equal(english.Keys.Order(), translated.Keys.Order());
        foreach (var (key, text) in english)
        {
            Assert.False(string.IsNullOrWhiteSpace(translated[key]), $"{language}: {key} is empty");
            Assert.True(
                Placeholders(text).SequenceEqual(Placeholders(translated[key])),
                $"{language}: {key} has placeholders [{string.Join(' ', Placeholders(translated[key]))}], English [{string.Join(' ', Placeholders(text))}]");

            // string.Format must accept it, or the app would crash at run time.
            _ = string.Format(System.Globalization.CultureInfo.InvariantCulture, translated[key], 1, DateTime.UnixEpoch);
        }

        Assert.Equal("ECI Support Centre", translated["AppName"]);
    }

    private static Dictionary<string, string> Read(string file) =>
        XDocument.Load(Path.Combine(StringsFolder, file)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    private static IEnumerable<string> Placeholders(string text) =>
        Placeholder().Matches(text).Select(m => m.Value).Order(StringComparer.Ordinal);

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "EciSupportCentre.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root (EciSupportCentre.slnx) not found.");
    }

    [GeneratedRegex(@"\{\d+(:[^}]*)?\}")]
    private static partial Regex Placeholder();
}
