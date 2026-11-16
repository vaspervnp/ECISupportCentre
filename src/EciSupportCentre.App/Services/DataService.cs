using EciSupportCentre.App.Localization;
using EciSupportCentre.Core.Sync;
using EciSupportCentre.Data;

namespace EciSupportCentre.App.Services;

/// <summary>
/// Single entry point of the UI to the local database and the sync. Runs at most one sync at a time
/// and tells the pages when stored data changed.
/// </summary>
public sealed class DataService(EciDatabase database, SyncEngine sync, NotificationService notifications, IConnectivity connectivity) : IDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _initialized;

    public EciDatabase Database => database;

    public bool IsSyncing { get; private set; }

    /// <summary>True when the device has no internet access (as seen by the app in the foreground).</summary>
    public bool IsOffline => connectivity.NetworkAccess != NetworkAccess.Internet;

    private bool IsUnmetered => connectivity.ConnectionProfiles.Any(p => p is ConnectionProfile.WiFi or ConnectionProfile.Ethernet);

    /// <summary>Message of the last failed sync, cleared by a successful one.</summary>
    public string? LastError { get; private set; }

    /// <summary>The last sync did not start because the device was offline (a state, not a failure).</summary>
    public bool LastErrorIsOffline { get; private set; }

    /// <summary>Raised (on any thread) after a sync or a user action changed stored data.</summary>
    public event EventHandler? DataChanged;

    /// <summary>Raised (on any thread) when <see cref="IsSyncing"/> or <see cref="LastError"/> changes.</summary>
    public event EventHandler? SyncStateChanged;

    public async Task EnsureReadyAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_initialized)
            {
                await database.InitializeAsync().ConfigureAwait(false);
                _initialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>Syncs now. If a sync is already running, waits for it and returns null.</summary>
    /// <param name="checkConnectivity">
    /// False for background jobs: WorkManager starts them only with a network, but the connectivity API reports
    /// background apps as offline. A real network failure still ends in <see cref="LastError"/>.
    /// </param>
    public async Task<SyncReport?> SyncAsync(bool checkConnectivity = true, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync().ConfigureAwait(false);

        if (!await _syncLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            _syncLock.Release();
            return null;
        }

        try
        {
            if (checkConnectivity && connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                SetState(false, Loc.Get("Sync_Offline"), offline: true);
                return null;
            }

            SetState(true, null);
            var report = await sync.SyncAsync(Loc.Instance.Language, cancellationToken: cancellationToken).ConfigureAwait(false);
            await notifications.NotifyAsync(report, cancellationToken).ConfigureAwait(false);
            SetState(false, null);
            DataChanged?.Invoke(this, EventArgs.Empty);
            return report;
        }
        catch (Exception ex) when (ex is HttpRequestException or Core.Api.EciApiException or TaskCanceledException)
        {
            SetState(false, Loc.Format("Sync_Failed", ex.Message));
            return null;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Syncs when the data is older than <paramref name="maxAge"/>, or was never synced. With "Wi-Fi only" this
    /// automatic sync waits for Wi-Fi, except the very first one, without which the app would stay empty.
    /// </summary>
    public async Task SyncIfStaleAsync(TimeSpan maxAge)
    {
        await EnsureReadyAsync().ConfigureAwait(false);
        var last = await database.GetLastSyncAsync().ConfigureAwait(false);
        if (last is not null && AppSettings.WifiOnly && !IsUnmetered)
        {
            return;
        }

        if (last is null || DateTimeOffset.UtcNow - last > maxAge)
        {
            await SyncAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Downloads missing or outdated details of one initiative. Returns false when offline or on error.</summary>
    public async Task<bool> EnsureDetailsAsync(long initiativeId)
    {
        await EnsureReadyAsync().ConfigureAwait(false);
        if (connectivity.NetworkAccess != NetworkAccess.Internet)
        {
            return false;
        }

        try
        {
            await sync.EnsureDetailsAsync(initiativeId).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or Core.Api.EciApiException or TaskCanceledException)
        {
            return false;
        }
    }

    public async Task SetFollowedAsync(long initiativeId, bool followed)
    {
        await EnsureReadyAsync().ConfigureAwait(false);
        await database.SetFollowedAsync(initiativeId, followed, DateTimeOffset.UtcNow).ConfigureAwait(false);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetSignedAsync(long initiativeId, bool hasSigned)
    {
        await EnsureReadyAsync().ConfigureAwait(false);
        await database.SetSignedAsync(initiativeId, hasSigned, DateTimeOffset.UtcNow).ConfigureAwait(false);
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _initLock.Dispose();
        _syncLock.Dispose();
    }

    private void SetState(bool syncing, string? error, bool offline = false)
    {
        IsSyncing = syncing;
        LastError = error;
        LastErrorIsOffline = offline;
        SyncStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
