using System.ComponentModel;
using System.Globalization;
using System.Resources;
using EciSupportCentre.Core;
using EciSupportCentre.Core.Localization;
using EciSupportCentre.Core.Models;

namespace EciSupportCentre.App.Localization;

/// <summary>
/// UI strings from <c>Resources/Strings/AppResources*.resx</c>. XAML binds to the indexer through
/// <see cref="TranslateExtension"/>, so changing <see cref="Culture"/> updates the UI without a restart.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    private static readonly ResourceManager Resources =
        new("EciSupportCentre.App.Resources.Strings.AppResources", typeof(Loc).Assembly);

    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    private Loc()
    {
    }

    public static Loc Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised once after the language changed (on the thread that changed it).</summary>
    public event EventHandler? LanguageChanged;

    /// <summary>The device's culture, captured before the app changes the current culture.</summary>
    public static CultureInfo DeviceCulture { get; } = CultureInfo.CurrentUICulture;

    /// <summary>EU language code of <see cref="Culture"/>, English when the culture is not an EU language.</summary>
    public string Language => EciSources.Normalize(_culture.Name);

    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (Equals(_culture, value))
            {
                return;
            }

            _culture = value;
            CultureInfo.CurrentUICulture = value;
            CultureInfo.CurrentCulture = value;
            CultureInfo.DefaultThreadCurrentUICulture = value;
            CultureInfo.DefaultThreadCurrentCulture = value;

            // An empty name means "everything changed": MAUI then refreshes every binding to this object,
            // including the indexer ones of {l:Translate}. ("Item[]", WPF's convention, is ignored by MAUI.)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Applies the user's choice (an EU language code, or empty for the device language).</summary>
    public void Apply(string? preference) =>
        Culture = CultureInfo.GetCultureInfo(LanguageChoice.Resolve(preference, DeviceCulture.Name));

    public string this[string key] => Resources.GetString(key, _culture) ?? key;

    public static string Get(string key) => Instance[key];

    public static string Format(string key, params object?[] args) =>
        string.Format(Instance._culture, Instance[key], args);

    public static string Phase(InitiativePhase phase) => Get($"Phase_{phase}");

    /// <summary>Name of a timeline step such as <c>COLLECTION_START_DATE</c>.</summary>
    public static string Step(string step)
    {
        var text = Resources.GetString($"Step_{step}", Instance._culture);
        return text ?? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(step.Replace('_', ' ').ToLowerInvariant());
    }

    /// <summary>Name of an answer document such as <c>COMMUNICATION</c>.</summary>
    public static string Document(string? kind) =>
        (kind is null ? null : Resources.GetString($"Doc_{kind}", Instance._culture)) ?? Get("Doc_Other");
}

/// <summary><c>Text="{l:Translate Key}"</c> binds to <see cref="Loc"/> and follows language changes.</summary>
[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public sealed class TranslateExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = "";

    public BindingBase ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]", BindingMode.OneWay, source: Loc.Instance);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
