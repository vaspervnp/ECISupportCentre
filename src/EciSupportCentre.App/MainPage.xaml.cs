using System.Globalization;
using System.Text;
using EciSupportCentre.Core;
using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Storage;
using EciSupportCentre.Core.Sync;
using EciSupportCentre.Data;

namespace EciSupportCentre.App;

public partial class MainPage : ContentPage
{
    private readonly EciDatabase _database;
    private readonly SyncEngine _sync;
    private bool _initialized;

    public MainPage(EciDatabase database, SyncEngine sync)
    {
        _database = database;
        _sync = sync;
        InitializeComponent();
    }

    private static string Language => EciSources.Normalize(CultureInfo.CurrentUICulture.Name);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await EnsureDatabaseAsync();
            await ShowStoredDataAsync(null);
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Database error: {ex.Message}";
        }
    }

    private async void OnSyncClicked(object? sender, EventArgs e)
    {
        SetBusy(true);
        try
        {
            await EnsureDatabaseAsync();
            var report = await _sync.SyncAsync(Language);
            await ShowStoredDataAsync(report);
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Sync failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnOpenWebsiteClicked(object? sender, EventArgs e) =>
        await Browser.Default.OpenAsync(EciSources.Website(Language), BrowserLaunchMode.SystemPreferred);

    private async Task EnsureDatabaseAsync()
    {
        if (!_initialized)
        {
            await _database.InitializeAsync();
            _initialized = true;
        }
    }

    private async Task ShowStoredDataAsync(SyncReport? report)
    {
        var items = await _database.GetInitiativesAsync(Language);
        var news = await _database.GetNewsAsync(Language);
        var lastSync = await _database.GetLastSyncAsync();

        var status = new StringBuilder();
        status.AppendLine(CultureInfo.CurrentCulture, $"Stored: {items.Count} initiatives, {items.Count(i => i.HasDetails)} with details, {news.Count} news.");
        status.AppendLine(CultureInfo.CurrentCulture, $"Last sync: {lastSync?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "never"}");
        if (report is not null)
        {
            status.AppendLine(CultureInfo.CurrentCulture, $"This sync: {(report.FirstSync ? "first" : "update")}, {report.Changes.Count} changes, {report.DetailsFetched} details ({report.DetailsPending} pending, {report.DetailsFailed.Count} failed), {report.NewsAdded ?? 0} new news, {(report.FinishedAt - report.StartedAt).TotalSeconds:0.0} s.");
            if (report.NewsError is not null)
            {
                status.AppendLine(CultureInfo.CurrentCulture, $"News error: {report.NewsError}");
            }
        }

        StatusLabel.Text = status.ToString().TrimEnd();

        var ongoing = new InitiativeQuery
        {
            Language = Language,
            Phases = new HashSet<InitiativePhase> { InitiativePhase.Collecting },
            Sort = InitiativeSort.EndingSoon,
        }.Apply(items);

        OngoingLabel.Text = ongoing.Count == 0
            ? ""
            : "Collecting signatures:\n" + string.Join(
                "\n",
                ongoing.Select(i => string.Create(CultureInfo.CurrentCulture, $"• {i.Title} — {i.TotalSupporters:N0}, ends {i.CollectionEndDate?.ToString("d", CultureInfo.CurrentCulture) ?? "?"}")));
    }

    private void SetBusy(bool busy)
    {
        SyncButton.IsEnabled = !busy;
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
    }
}
