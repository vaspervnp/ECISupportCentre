using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EciSupportCentre.App.Localization;
using EciSupportCentre.App.Services;
using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Storage;

namespace EciSupportCentre.App.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly DataService _data;

    public HomeViewModel(DataService data)
    {
        _data = data;
        _data.DataChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());
        _data.SyncStateChanged += (_, _) => MainThread.BeginInvokeOnMainThread(UpdateSyncState);
        Connectivity.Current.ConnectivityChanged += (_, _) => MainThread.BeginInvokeOnMainThread(UpdateSyncState);
        Loc.Instance.LanguageChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());
    }

    public ObservableCollection<InitiativeItemViewModel> EndingSoon { get; } = [];

    [ObservableProperty]
    public partial string Registered { get; set; } = "–";

    [ObservableProperty]
    public partial string Collecting { get; set; } = "–";

    [ObservableProperty]
    public partial string Successful { get; set; } = "–";

    [ObservableProperty]
    public partial string Answered { get; set; } = "–";

    [ObservableProperty]
    public partial string Failed { get; set; } = "–";

    [ObservableProperty]
    public partial string Withdrawn { get; set; } = "–";

    [ObservableProperty]
    public partial string SyncText { get; set; } = "";

    /// <summary>At least one initiative is stored.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState), nameof(CanDownload))]
    public partial bool HasData { get; set; } = true;

    [ObservableProperty]
    public partial bool HasEndingSoon { get; set; }

    /// <summary>No data yet and the first sync is running.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    public partial bool IsDownloading { get; set; }

    public bool ShowEmptyState => !HasData;

    public bool CanDownload => !HasData && !IsDownloading;

    /// <summary>How to get the data: pull or tap, or first connect to the internet.</summary>
    [ObservableProperty]
    public partial string EmptyText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    public async Task OnAppearingAsync()
    {
        await LoadAsync();
        await AskForNotificationsOnceAsync();

        // Refresh in the background when the data is older than six hours.
        _ = _data.SyncIfStaleAsync(TimeSpan.FromHours(6));
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

    /// <summary>Android 13+ needs the user's permission to show notifications; ask once, after the first data arrived.</summary>
    private static async Task AskForNotificationsOnceAsync()
    {
        const string askedKey = "asked_notification_permission";
        if (Preferences.Default.Get(askedKey, false))
        {
            return;
        }

        Preferences.Default.Set(askedKey, true);
        if (await Permissions.CheckStatusAsync<Permissions.PostNotifications>() != PermissionStatus.Granted)
        {
            await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
    }

    [RelayCommand]
    private static Task SeeAllAsync() => Shell.Current.GoToAsync("//initiatives");

    /// <summary>
    /// Counts loads; a load that finishes after a newer one started drops its (older) results. Without it, a load
    /// begun before the first sync saved the list could finish last and show the app as empty again.
    /// </summary>
    private int _loadVersion;

    private async Task LoadAsync()
    {
        var version = ++_loadVersion;
        await _data.EnsureReadyAsync();
        // Microsoft.Data.Sqlite runs its "async" calls synchronously: read on a pool thread, not the UI thread.
        var language = Loc.Instance.Language;
        var (items, lastSync) = await Task.Run(async () =>
            (await _data.Database.GetInitiativesAsync(language), await _data.Database.GetLastSyncAsync()));
        if (version != _loadVersion)
        {
            return;
        }

        int Count(params InitiativePhase[] phases) => items.Count(i => phases.Contains(i.Phase));
        string N(int n) => n.ToString("N0", Loc.Instance.Culture);

        Registered = N(items.Count);
        Collecting = N(Count(InitiativePhase.Collecting));
        Successful = N(Count(InitiativePhase.Submitted, InitiativePhase.Answered));
        Answered = N(Count(InitiativePhase.Answered));
        Failed = N(Count(InitiativePhase.Failed));
        Withdrawn = N(Count(InitiativePhase.Withdrawn));

        var soon = new InitiativeQuery
        {
            Phases = new HashSet<InitiativePhase> { InitiativePhase.Collecting },
            Sort = InitiativeSort.EndingSoon,
        }.Apply(items).Take(5);

        EndingSoon.Clear();
        foreach (var item in soon)
        {
            EndingSoon.Add(new InitiativeItemViewModel(item, i => AppRoutes.OpenInitiativeAsync(i.Id)));
        }

        HasData = items.Count > 0;
        HasEndingSoon = EndingSoon.Count > 0;

        SyncText = lastSync is { } at ? Loc.Format("Sync_Last", Presentation.RelativeTime(at)) : Loc.Get("Sync_Never");
        UpdateSyncState();
    }

    private void UpdateSyncState()
    {
        if (_data.IsSyncing)
        {
            SyncText = Loc.Get("Sync_Running");
        }

        IsDownloading = _data.IsSyncing && !HasData;
        // The banner is hidden while there is no data, so a failed first download is explained here.
        EmptyText = _data.IsOffline ? Loc.Get("Home_EmptyOffline")
            : _data.LastError is { } error && !_data.LastErrorIsOffline ? error
            : Loc.Get("Home_Empty");
    }
}

/// <summary>Shell routes used by the view models.</summary>
internal static class AppRoutes
{
    public const string InitiativeRoute = "initiative";

    public const string AboutRoute = "about";

    public static Task OpenInitiativeAsync(long id) =>
        Shell.Current.GoToAsync($"{InitiativeRoute}?id={id.ToString(CultureInfo.InvariantCulture)}");
}
