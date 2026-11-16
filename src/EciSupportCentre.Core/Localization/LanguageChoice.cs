namespace EciSupportCentre.Core.Localization;

/// <summary>The 24 official EU languages the app offers, and which culture to use for a choice.</summary>
public static class LanguageChoice
{
    /// <summary>One language in the picker: its code and its name in the language itself.</summary>
    public sealed record Language(string Code, string NativeName, string DefaultCulture);

    /// <summary>
    /// In the order the EU's own websites list them (alphabetical by the name in each language).
    /// The default culture gives dates and numbers when the device uses another language.
    /// </summary>
    public static IReadOnlyList<Language> All { get; } =
    [
        new("bg", "български", "bg-BG"),
        new("es", "español", "es-ES"),
        new("cs", "čeština", "cs-CZ"),
        new("da", "dansk", "da-DK"),
        new("de", "Deutsch", "de-DE"),
        new("et", "eesti", "et-EE"),
        new("el", "ελληνικά", "el-GR"),
        new("en", "English", "en-IE"),
        new("fr", "français", "fr-FR"),
        new("ga", "Gaeilge", "ga-IE"),
        new("hr", "hrvatski", "hr-HR"),
        new("it", "italiano", "it-IT"),
        new("lv", "latviešu", "lv-LV"),
        new("lt", "lietuvių", "lt-LT"),
        new("hu", "magyar", "hu-HU"),
        new("mt", "Malti", "mt-MT"),
        new("nl", "Nederlands", "nl-NL"),
        new("pl", "polski", "pl-PL"),
        new("pt", "português", "pt-PT"),
        new("ro", "română", "ro-RO"),
        new("sk", "slovenčina", "sk-SK"),
        new("sl", "slovenščina", "sl-SI"),
        new("fi", "suomi", "fi-FI"),
        new("sv", "svenska", "sv-SE"),
    ];

    /// <summary>
    /// Culture to use. <paramref name="preference"/> is an EU language code, or null/empty to follow the device.
    /// A device culture of the same language is kept, so its regional formats stay (e.g. de-AT, en-GB);
    /// a device language outside the EU gives English with Irish (EU) formats.
    /// </summary>
    public static string Resolve(string? preference, string? deviceCulture)
    {
        var device = deviceCulture ?? "";
        var deviceLanguage = device.Split('-', '_')[0].ToLowerInvariant();

        var wanted = Find(preference) ?? Find(deviceLanguage) ?? Find(EciSources.DefaultLanguage)!;
        return wanted.Code == deviceLanguage && device.Length > 0 ? device.Replace('_', '-') : wanted.DefaultCulture;
    }

    /// <summary>The language with this code, or null when it is not an EU language.</summary>
    public static Language? Find(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : All.FirstOrDefault(l => string.Equals(l.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
}
