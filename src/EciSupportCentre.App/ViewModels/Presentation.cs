using EciSupportCentre.App.Localization;
using EciSupportCentre.Core;
using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Presentation;
using EciSupportCentre.Core.Storage;

namespace EciSupportCentre.App.ViewModels;

/// <summary>Translated texts and colours for initiatives.</summary>
internal static class Presentation
{
    public static Color PhaseColor(InitiativePhase phase) => phase switch
    {
        InitiativePhase.Collecting => Color.FromArgb("#1B7F3B"),
        InitiativePhase.Upcoming => Color.FromArgb("#0E6FA8"),
        InitiativePhase.Verification => Color.FromArgb("#9A6700"),
        InitiativePhase.Submitted => Color.FromArgb("#5B3FA8"),
        InitiativePhase.Answered => Color.FromArgb("#003399"),
        InitiativePhase.Failed => Color.FromArgb("#B42318"),
        _ => Color.FromArgb("#5F6368"),
    };

    public static string Supporters(long count) => Loc.Format("Item_Supporters", count);

    public static string DateLine(InitiativeListItem item)
    {
        var (kind, date) = InitiativePresentation.DateLine(item, DateOnly.FromDateTime(DateTime.Today));
        return kind switch
        {
            DateLineKind.StartsOn => Loc.Format("Item_StartsOn", date!.Value.ToDateTime(TimeOnly.MinValue)),
            DateLineKind.EndsOn => Loc.Format("Item_EndsOn", date!.Value.ToDateTime(TimeOnly.MinValue)),
            DateLineKind.EndedOn => Loc.Format("Item_EndedOn", date!.Value.ToDateTime(TimeOnly.MinValue)),
            _ => "",
        };
    }

    public static ImageSource? Logo(long? logoId) => logoId is { } id
        ? new UriImageSource { Uri = EciSources.Logo(id), CachingEnabled = true, CacheValidity = TimeSpan.FromDays(30) }
        : null;

    public static string Date(DateOnly? date) =>
        date?.ToString("d", Loc.Instance.Culture) ?? "";

    /// <summary>Country name in the app language (Android's locale data covers all EU languages).</summary>
    public static string CountryName(string euCode)
    {
        var iso = InitiativePresentation.ToIsoCountry(euCode);
        using var builder = new Java.Util.Locale.Builder();
        using var country = builder.SetRegion(iso)!.Build();
        using var language = Java.Util.Locale.ForLanguageTag(Loc.Instance.Culture.Name);
        var name = country.GetDisplayCountry(language);
        return string.IsNullOrEmpty(name) || name == iso ? euCode : name;
    }

    public static string RelativeTime(DateTimeOffset when)
    {
        var local = when.ToLocalTime();
        return local.Date == DateTime.Today
            ? local.ToString("t", Loc.Instance.Culture)
            : local.ToString("g", Loc.Instance.Culture);
    }
}
