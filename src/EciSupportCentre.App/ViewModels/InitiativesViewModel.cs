using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EciSupportCentre.App.Localization;
using EciSupportCentre.App.Services;
using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Storage;

namespace EciSupportCentre.App.ViewModels;

/// <summary>A filter chip above the list.</summary>
public sealed partial class FilterOption(string labelKey, IReadOnlySet<InitiativePhase> phases, bool followedOnly = false) : ObservableObject
{
    public string LabelKey { get; } = labelKey;

    public string Label => Loc.Get(LabelKey);

    public IReadOnlySet<InitiativePhase> Phases { get; } = phases;

    public bool FollowedOnly { get; } = followedOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Background), nameof(Foreground))]
    public partial bool IsSelected { get; set; }

    public Color Background => IsSelected ? Color.FromArgb("#003399") : Colors.Transparent;

    public Color Foreground => IsSelected ? Colors.White : Color.FromArgb("#003399");

    public void RefreshLabel() => OnPropertyChanged(nameof(Label));
}

public sealed partial class InitiativesViewModel : ObservableObject
{
    private static readonly InitiativeSort[] SortValues =
        [InitiativeSort.Newest, InitiativeSort.MostSupporters, InitiativeSort.EndingSoon, InitiativeSort.RecentlyUpdated];

    private readonly DataService _data;
    private IReadOnlyList<InitiativeListItem> _all = [];

    public InitiativesViewModel(DataService data, InitiativeDetailViewModel detail)
    {
        _data = data;
        Detail = detail;
        Filters =
        [
            new FilterOption("List_All", new HashSet<InitiativePhase>()) { IsSelected = true },
            new FilterOption("Phase_Collecting", new HashSet<InitiativePhase> { InitiativePhase.Collecting }),
            new FilterOption("Phase_Upcoming", new HashSet<InitiativePhase> { InitiativePhase.Upcoming }),
            new FilterOption("Home_Successful", new HashSet<InitiativePhase> { InitiativePhase.Verification, InitiativePhase.Submitted, InitiativePhase.Answered }),
            new FilterOption("Phase_Failed", new HashSet<InitiativePhase> { InitiativePhase.Failed }),
            new FilterOption("Phase_Withdrawn", new HashSet<InitiativePhase> { InitiativePhase.Withdrawn }),
            new FilterOption("List_Followed", new HashSet<InitiativePhase>(), followedOnly: true),
        ];
        SortOptions = [.. SortValues.Select(s => Loc.Get($"Sort_{s}"))];

        _data.DataChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());
        Loc.Instance.PropertyChanged += (_, _) => MainThread.BeginInvokeOnMainThread(OnLanguageChanged);
    }

    public ObservableCollection<InitiativeItemViewModel> Items { get; } = [];

    public IReadOnlyList<FilterOption> Filters { get; }

    [ObservableProperty]
    public partial IReadOnlyList<string> SortOptions { get; set; }

    /// <summary>Details shown next to the list on wide screens.</summary>
    public InitiativeDetailViewModel Detail { get; }

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    [ObservableProperty]
    public partial int SortIndex { get; set; }

    [ObservableProperty]
    public partial string CountText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    /// <summary>Set by the page from its width: list and details side by side.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDetailPlaceholder))]
    public partial bool IsWide { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDetailPlaceholder))]
    public partial long? SelectedId { get; set; }

    public bool ShowDetailPlaceholder => IsWide && SelectedId is null;

    private FilterOption SelectedFilter => Filters.First(f => f.IsSelected);

    public Task OnAppearingAsync() => _all.Count == 0 ? LoadAsync() : Task.CompletedTask;

    partial void OnSearchTextChanged(string? value) => ApplyQuery();

    partial void OnSortIndexChanged(int value) => ApplyQuery();

    [RelayCommand]
    private void SelectFilter(FilterOption option)
    {
        foreach (var filter in Filters)
        {
            filter.IsSelected = ReferenceEquals(filter, option);
        }

        ApplyQuery();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            await _data.SyncAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task LoadAsync()
    {
        await _data.EnsureReadyAsync();
        _all = await _data.Database.GetInitiativesAsync(Loc.Instance.Language);
        ApplyQuery();
    }

    private void ApplyQuery()
    {
        var filter = SelectedFilter;
        var query = new InitiativeQuery
        {
            Language = Loc.Instance.Language,
            Phases = filter.Phases,
            FollowedOnly = filter.FollowedOnly,
            Search = SearchText,
            Sort = SortValues[Math.Clamp(SortIndex, 0, SortValues.Length - 1)],
        };

        var results = query.Apply(_all);
        Items.Clear();
        foreach (var item in results)
        {
            Items.Add(new InitiativeItemViewModel(item, OpenAsync) { IsSelected = item.Id == SelectedId });
        }

        CountText = Loc.Format("List_Count", results.Count);
        IsEmpty = results.Count == 0;
    }

    private async Task OpenAsync(InitiativeItemViewModel item)
    {
        if (!IsWide)
        {
            await AppRoutes.OpenInitiativeAsync(item.Id);
            return;
        }

        SelectedId = item.Id;
        foreach (var other in Items)
        {
            other.IsSelected = other.Id == item.Id;
        }

        await Detail.LoadAsync(item.Id);
    }

    private void OnLanguageChanged()
    {
        foreach (var filter in Filters)
        {
            filter.RefreshLabel();
        }

        var sort = SortIndex;
        SortOptions = [.. SortValues.Select(s => Loc.Get($"Sort_{s}"))];
        SortIndex = sort;
        _ = LoadAsync();
    }
}
