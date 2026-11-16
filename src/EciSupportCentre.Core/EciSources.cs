namespace EciSupportCentre.Core;

/// <summary>
/// Public data sources of the European Citizens' Initiative.
/// </summary>
public static class EciSources
{
    public const string DefaultLanguage = "en";

    /// <summary>The 24 official languages of the European Union (ISO 639-1).</summary>
    public static IReadOnlyList<string> Languages { get; } =
    [
        "bg", "cs", "da", "de", "el", "en", "es", "et", "fi", "fr", "ga", "hr",
        "hu", "it", "lt", "lv", "mt", "nl", "pl", "pt", "ro", "sk", "sl", "sv",
    ];

    public static Uri RegisterApiBase { get; } = new("https://register.eci.ec.europa.eu/core/api/register/");

    public static Uri NewsFeed { get; } = new("https://citizens-initiative.europa.eu/node/418/rss_en");

    /// <summary>Returns the public website home page in the given language, or in English if the language is not an EU language.</summary>
    public static Uri Website(string language) =>
        new($"https://citizens-initiative.europa.eu/index_{Normalize(language)}");

    /// <summary>Returns the EU language code for a culture name such as "el-GR", or English when it is not supported.</summary>
    public static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return DefaultLanguage;
        }

        var code = language.Split('-', '_')[0].ToLowerInvariant();
        return Languages.Contains(code) ? code : DefaultLanguage;
    }
}
