using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EciSupportCentre.App.Localization;
using EciSupportCentre.App.Services;
using EciSupportCentre.Core;
using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Presentation;
using EciSupportCentre.Core.Storage;

namespace EciSupportCentre.App.ViewModels;

public sealed record TimelineEntry(string Text, string Date, bool IsActive)
{
    public FontAttributes Font => IsActive ? FontAttributes.Bold : FontAttributes.None;

    public Color Dot => IsActive ? Color.FromArgb("#003399") : Color.FromArgb("#ACACAC");
}

public sealed record CountryEntry(string Name, string Count, double Fraction);

public sealed record LinkEntry(string Label, Uri Uri);

public sealed partial class InitiativeDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly DataService _data;
    private long _id;
    private bool _showing;
    private int _loadVersion;

    public InitiativeDetailViewModel(DataService data)
    {
        _data = data;
    }

    public ObservableCollection<TimelineEntry> Timeline { get; } = [];

    public ObservableCollection<CountryEntry> Countries { get; } = [];

    public ObservableCollection<LinkEntry> Documents { get; } = [];

    [ObservableProperty]
    public partial bool HasItem { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial string RegNumber { get; set; } = "";

    [ObservableProperty]
    public partial string PhaseText { get; set; } = "";

    [ObservableProperty]
    public partial Color PhaseColor { get; set; } = Colors.Gray;

    [ObservableProperty]
    public partial ImageSource? Logo { get; set; }

    [ObservableProperty]
    public partial string SupportersText { get; set; } = "";

    [ObservableProperty]
    public partial string DatesText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsFollowed { get; set; }

    [ObservableProperty]
    public partial string FollowText { get; set; } = "";

    /// <summary>Official signing page in the app language; null when the initiative cannot be signed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSign), nameof(ShowSignedMark))]
    public partial Uri? SigningPage { get; set; }

    public bool CanSign => SigningPage is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSignedMark))]
    public partial bool IsSigned { get; set; }

    /// <summary>The mark is offered while signing is possible, and kept visible afterwards if set.</summary>
    public bool ShowSignedMark => CanSign || IsSigned;

    [ObservableProperty]
    public partial string? DeadlineText { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingDetails { get; set; }

    [ObservableProperty]
    public partial bool HasDetails { get; set; }

    [ObservableProperty]
    public partial string? DetailsMessage { get; set; }

    [ObservableProperty]
    public partial string? Objectives { get; set; }

    [ObservableProperty]
    public partial string? Annex { get; set; }

    [ObservableProperty]
    public partial string? Treaties { get; set; }

    [ObservableProperty]
    public partial string? SignaturesText { get; set; }

    [ObservableProperty]
    public partial string? AnswerText { get; set; }

    [ObservableProperty]
    public partial Uri? OfficialPage { get; set; }

    public bool HasCountries => Countries.Count > 0;

    public bool HasDocuments => Documents.Count > 0;

    public bool HasAnswer => !string.IsNullOrEmpty(AnswerText);

    public bool HasAnnex => !string.IsNullOrEmpty(Annex);

    public bool HasTreaties => !string.IsNullOrEmpty(Treaties);

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.TryGetValue("id", out var value) && long.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            _ = LoadAsync(id);
        }
    }

    public async Task LoadAsync(long id)
    {
        _id = id;
        var version = ++_loadVersion;
        await _data.EnsureReadyAsync();

        if (!await ShowStoredAsync(version))
        {
            return;
        }

        // Fetch details when missing or outdated, then show them.
        var item = await _data.Database.GetInitiativeAsync(id, Loc.Instance.Language);
        var stale = item is not null && (!item.HasDetails || (await _data.Database.GetSnapshotsAsync()).GetValueOrDefault(id)?.DetailsLatestUpdate != item.LatestUpdateDate);
        if (!stale)
        {
            return;
        }

        IsLoadingDetails = true;
        try
        {
            var fetched = await _data.EnsureDetailsAsync(id);
            if (version != _loadVersion)
            {
                return;
            }

            await ShowStoredAsync(version);
            if (!fetched && !HasDetails)
            {
                DetailsMessage = Loc.Get("Detail_Unavailable");
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoadingDetails = false;
            }
        }
    }

    [RelayCommand]
    private async Task ToggleFollowAsync()
    {
        await _data.SetFollowedAsync(_id, !IsFollowed);
        IsFollowed = !IsFollowed;
        FollowText = Loc.Get(IsFollowed ? "Detail_Unfollow" : "Detail_Follow");
    }

    /// <summary>Opens the Commission's collection system in a Custom Tab. Signing happens there, not in the app.</summary>
    [RelayCommand]
    private Task SignAsync() => AppBrowser.OpenAsync(SigningPage);

    /// <summary>Saves the switch state; the binding has already set <see cref="IsSigned"/>.</summary>
    partial void OnIsSignedChanged(bool value)
    {
        if (_showing)
        {
            return;
        }

        _ = _data.SetSignedAsync(_id, value);
    }

    [RelayCommand]
    private async Task OpenOfficialPageAsync()
    {
        if (OfficialPage is not null)
        {
            await Browser.Default.OpenAsync(OfficialPage, BrowserLaunchMode.SystemPreferred);
        }
    }

    [RelayCommand]
    private static async Task OpenLinkAsync(LinkEntry link) =>
        await Browser.Default.OpenAsync(link.Uri, BrowserLaunchMode.SystemPreferred);

    /// <summary>Shows what the database has. Returns false if the initiative is unknown or a newer load started.</summary>
    private async Task<bool> ShowStoredAsync(int version)
    {
        var language = Loc.Instance.Language;
        var item = await _data.Database.GetInitiativeAsync(_id, language);
        var details = item?.HasDetails == true ? await _data.Database.GetDetailsAsync(_id) : null;
        if (version != _loadVersion)
        {
            return false;
        }

        HasItem = item is not null;
        if (item is null)
        {
            return false;
        }

        ShowItem(item, language);
        ShowDetails(details, language);
        return true;
    }

    private void ShowItem(InitiativeListItem item, string language)
    {
        Title = item.Title;
        RegNumber = item.PubRegNum;
        PhaseText = Loc.Phase(item.Phase);
        PhaseColor = Presentation.PhaseColor(item.Phase);
        Logo = Presentation.Logo(item.LogoId);
        SupportersText = Presentation.Supporters(item.TotalSupporters);
        IsFollowed = item.IsFollowed;
        FollowText = Loc.Get(item.IsFollowed ? "Detail_Unfollow" : "Detail_Follow");
        OfficialPage = EciSources.InitiativePage(item.Year, item.Number, language);

        _showing = true;
        IsSigned = item.IsSigned;
        _showing = false;
        SigningPage = item.Status.CanBeSigned() ? EciSources.SigningPage(item.SupportLink, language) : null;
        DeadlineText = item.DaysLeft(DateOnly.FromDateTime(DateTime.Today)) switch
        {
            null => null,
            0 => Loc.Get("Sign_LastDay"),
            var days => Loc.Format("Sign_DaysLeft", days, item.CollectionEndDate!.Value.ToDateTime(TimeOnly.MinValue)),
        };

        var dates = new List<string>();
        if (item.RegistrationDate is { } registered)
        {
            dates.Add(Loc.Format("Detail_Registered", registered.ToDateTime(TimeOnly.MinValue)));
        }

        if (item.StartCollectionDate is { } start)
        {
            dates.Add(item.CollectionEndDate is { } end
                ? Loc.Format("Detail_Collection", start.ToDateTime(TimeOnly.MinValue), end.ToDateTime(TimeOnly.MinValue))
                : Loc.Format("Detail_CollectionOpen", start.ToDateTime(TimeOnly.MinValue)));
        }

        DatesText = string.Join(" · ", dates);
    }

    private void ShowDetails(InitiativeDetails? details, string language)
    {
        HasDetails = details is not null;
        DetailsMessage = null;
        Timeline.Clear();
        Countries.Clear();
        Documents.Clear();

        var version = details?.GetVersion(language);
        Objectives = version?.Objectives;
        Annex = version?.AnnexText;
        Treaties = version?.Treaties;

        if (details is not null)
        {
            if (!string.IsNullOrEmpty(version?.Title))
            {
                Title = version.Title;
            }

            foreach (var step in InitiativePresentation.Timeline(details))
            {
                Timeline.Add(new TimelineEntry(Loc.Step(step.Step), Presentation.Date(step.Date), step.Active));
            }

            SignaturesText = details.Submission is { TotalSignatures: > 0 } verified
                ? Loc.Format("Detail_SignaturesVerified", verified.TotalSignatures)
                : details.SosReport is { TotalSignatures: > 0 } collected
                    ? Loc.Format("Detail_SignaturesCollected", collected.TotalSignatures)
                    : null;

            var top = InitiativePresentation.TopCountries(details, 10);
            var max = top.Count > 0 ? top[0].Total : 1;
            foreach (var country in top)
            {
                Countries.Add(new CountryEntry(
                    Presentation.CountryName(country.CountryCode),
                    country.Total.ToString("N0", Loc.Instance.Culture),
                    (double)country.Total / max));
            }

            AnswerText = details.Answer?.DecisionDate is { } decided
                ? Loc.Format("Detail_AnswerDate", decided.ToDateTime(TimeOnly.MinValue))
                : null;

            foreach (var link in (details.Answer?.Links ?? []).Concat(details.PreAnswer?.Links ?? []))
            {
                if (InitiativePresentation.DocumentLink(link, language) is { } uri)
                {
                    Documents.Add(new LinkEntry(Loc.Document(link.DefaultName), uri));
                }
            }

            if (version?.CommissionDecision?.Url is { } decision)
            {
                Documents.Add(new LinkEntry(Loc.Get("Doc_Registration"), decision));
            }
        }
        else
        {
            SignaturesText = null;
            AnswerText = null;
        }

        OnPropertyChanged(nameof(HasCountries));
        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(HasAnswer));
        OnPropertyChanged(nameof(HasAnnex));
        OnPropertyChanged(nameof(HasTreaties));
    }
}
