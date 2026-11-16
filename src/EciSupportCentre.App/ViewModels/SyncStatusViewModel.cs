using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EciSupportCentre.App.Localization;
using EciSupportCentre.App.Services;

namespace EciSupportCentre.App.ViewModels;

/// <summary>
/// The banner at the top of the Home, Initiatives and News tabs: offline (saved data is shown) or the last
/// sync failed (with a retry). Hidden while a sync runs, since the pull-to-refresh spinner shows that.
/// </summary>
public sealed partial class SyncStatusViewModel : ObservableObject
{
    private readonly DataService _data;

    public SyncStatusViewModel(DataService data, IConnectivity connectivity)
    {
        _data = data;
        _data.SyncStateChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Update);
        connectivity.ConnectivityChanged += (_, _) => MainThread.BeginInvokeOnMainThread(OnConnectivityChanged);
        Loc.Instance.LanguageChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Update);
        Update();
    }

    [ObservableProperty]
    public partial string? Message { get; set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    /// <summary>A failed sync (red, with a retry button); otherwise the offline notice.</summary>
    [ObservableProperty]
    public partial bool IsError { get; set; }

    [RelayCommand]
    private async Task RetryAsync() => await _data.SyncAsync();

    private void OnConnectivityChanged()
    {
        Update();

        // Back online: catch up if the data got old meanwhile.
        if (!_data.IsOffline)
        {
            _ = _data.SyncIfStaleAsync(TimeSpan.FromHours(6));
        }
    }

    private void Update()
    {
        if (_data.IsSyncing)
        {
            Show(null, false);
        }
        else if (_data.IsOffline)
        {
            Show(Loc.Get("Sync_Offline"), false);
        }
        else if (_data.LastError is { } error && !_data.LastErrorIsOffline)
        {
            Show(error, true);
        }
        else
        {
            Show(null, false);
        }
    }

    private void Show(string? message, bool isError)
    {
        Message = message;
        IsError = isError;
        IsVisible = message is not null;
    }
}
