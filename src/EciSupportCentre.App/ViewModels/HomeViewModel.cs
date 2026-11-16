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
        Loc.Instance.PropertyChanged += (_, _) => _ = LoadAsync();
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

    [ObservableProperty]
    public partial string? ErrorText { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    /// <summary>Notify about every initiative; off means only followed ones.</summary>
    [ObservableProperty]
    public partial bool NotifyAll { get; set; } = AppSettings.FollowAll;

    partial void OnNotifyAllChanged(bool value) => AppSettings.FollowAll = value;

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

    private async Task LoadAsync()
    {
        await _data.EnsureReadyAsync();
        var items = await _data.Database.GetInitiativesAsync(Loc.Instance.Language);
        var lastSync = await _data.Database.GetLastSyncAsync();

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

        SyncText = lastSync is { } at ? Loc.Format("Sync_Last", Presentation.RelativeTime(at)) : Loc.Get("Sync_Never");
        UpdateSyncState();
    }

    private void UpdateSyncState()
    {
        ErrorText = _data.LastError;
        if (_data.IsSyncing)
        {
            SyncText = Loc.Get("Sync_Running");
        }
    }
}

/// <summary>Shell routes used by the view models.</summary>
internal static class AppRoutes
{
    public const string InitiativeRoute = "initiative";

    public static Task OpenInitiativeAsync(long id) =>
        Shell.Current.GoToAsync($"{InitiativeRoute}?id={id.ToString(CultureInfo.InvariantCulture)}");
}
