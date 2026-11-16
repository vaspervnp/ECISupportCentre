namespace EciSupportCentre.App.Services;

/// <summary>Background sync schedule (Android: WorkManager, see <c>BackgroundSync</c>).</summary>
public interface ISyncScheduler
{
    /// <summary>Applies <see cref="AppSettings.SyncIntervalHours"/> and <see cref="AppSettings.WifiOnly"/>.</summary>
    void Reschedule();
}
