using Android.Content;
using Android.Runtime;
using AndroidX.Work;
using EciSupportCentre.App.Services;
using Java.Util.Concurrent;

namespace EciSupportCentre.App;

/// <summary>Periodic background sync with WorkManager; survives reboots and respects Doze and battery saver.</summary>
public static class BackgroundSync
{
    private const string WorkName = "eci-sync";

    /// <summary>
    /// Schedules the sync, or updates the schedule when the settings changed. A running job is not interrupted.
    /// </summary>
    public static void Schedule(Context context)
    {
        var request = (PeriodicWorkRequest)new PeriodicWorkRequest.Builder(
                Java.Lang.Class.FromType(typeof(SyncWorker)), AppSettings.SyncIntervalHours, TimeUnit.Hours!)
            .SetConstraints(NetworkConstraints())
            .SetBackoffCriteria(BackoffPolicy.Exponential!, 30, TimeUnit.Minutes!)
            .Build();

        WorkManager.GetInstance(context).EnqueueUniquePeriodicWork(WorkName, ExistingPeriodicWorkPolicy.Update!, request);
    }

    /// <summary>
    /// Run only with a network. This also matters for access: Android gives a background job the network
    /// only when the job asked for it.
    /// </summary>
    private static Constraints NetworkConstraints() => new Constraints.Builder()
        .SetRequiredNetworkType(AppSettings.WifiOnly ? NetworkType.Unmetered! : NetworkType.Connected!)
        .Build();

#if DEBUG
    /// <summary>
    /// Debug builds only: runs <see cref="SyncWorker"/> once after a short delay, so the background path can be tried
    /// without waiting for the period: <c>adb shell am start -n io.github.vaspervnp.ecisupportcentre/crc641f9ab5b7e96c61fb.MainActivity --ez eci_debug_sync true</c>.
    /// </summary>
    public static void RunOnceSoon(Context context)
    {
        var builder = new OneTimeWorkRequest.Builder(Java.Lang.Class.FromType(typeof(SyncWorker)));
        builder.SetInitialDelay(15, TimeUnit.Seconds!);
        builder.SetConstraints(NetworkConstraints());
        WorkManager.GetInstance(context).Enqueue(builder.Build());
    }
#endif
}

/// <summary>Reschedules <see cref="BackgroundSync"/> after the user changed the sync settings.</summary>
public sealed class AndroidSyncScheduler : ISyncScheduler
{
    public void Reschedule() => BackgroundSync.Schedule(Android.App.Application.Context);
}

/// <summary>One background sync: register, details, news, then notifications (inside <see cref="DataService.SyncAsync"/>).</summary>
[Register("io.github.vaspervnp.ecisupportcentre.SyncWorker")]
public sealed class SyncWorker(Context context, WorkerParameters parameters) : Worker(context, parameters)
{
    public override Result DoWork()
    {
        // MauiApplication.OnCreate has built the services before WorkManager runs any job.
        if (IPlatformApplication.Current?.Services.GetService<DataService>() is not { } data)
        {
            return Result.InvokeRetry();
        }

        var report = data.SyncAsync(checkConnectivity: false).GetAwaiter().GetResult();
        if (report is null && data.LastError is { } error)
        {
            Android.Util.Log.Warn("EciSync", error);
        }

        // Null with an error: offline or the register failed; try again later. Null without one: another sync was running.
        return report is null && data.LastError is not null ? Result.InvokeRetry() : Result.InvokeSuccess();
    }
}
