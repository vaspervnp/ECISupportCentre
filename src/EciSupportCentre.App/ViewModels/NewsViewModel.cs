using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EciSupportCentre.App.Localization;
using EciSupportCentre.App.Services;
using EciSupportCentre.Core.Models;
using EciSupportCentre.Core.Presentation;

namespace EciSupportCentre.App.ViewModels;

/// <summary>The News tab: the stored news feed, refreshed by every sync.</summary>
public sealed partial class NewsViewModel : ObservableObject
{
    private readonly DataService _data;

    public NewsViewModel(DataService data)
    {
        _data = data;
        _data.DataChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() => _ = LoadAsync());
        _data.SyncStateChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() => ErrorText = _data.LastError);
        Loc.Instance.PropertyChanged += (_, _) => _ = LoadAsync();
    }

    public ObservableCollection<NewsItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial string? ErrorText { get; set; }

    /// <summary>Set once the database was read, so the empty message does not flash while loading.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public async Task OnAppearingAsync()
    {
        await LoadAsync();
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

    private async Task LoadAsync()
    {
        await _data.EnsureReadyAsync();
        var news = await _data.Database.GetNewsAsync(Loc.Instance.Language);

        Items.Clear();
        foreach (var item in news)
        {
            Items.Add(new NewsItemViewModel(item, OpenAsync));
        }

        IsEmpty = Items.Count == 0;
        ErrorText = _data.LastError;
    }

    private async Task OpenAsync(NewsItemViewModel item)
    {
        if (!item.IsRead)
        {
            item.IsRead = true;
            await _data.Database.SetNewsReadAsync(item.Id);
        }

        await AppBrowser.OpenAsync(item.Link);
    }
}

/// <summary>One article in the News tab.</summary>
public sealed partial class NewsItemViewModel : ObservableObject
{
    /// <summary>How long an unread article counts as new.</summary>
    private static readonly TimeSpan RecentFor = TimeSpan.FromDays(14);

    public NewsItemViewModel(NewsItem item, Func<NewsItemViewModel, Task> open)
    {
        Id = item.Id;
        Link = item.Link;
        Title = item.Title;
        Summary = HtmlText.ToPlainText(item.DescriptionHtml);
        DateText = item.Published is { } published
            ? published.ToLocalTime().ToString("D", Loc.Instance.Culture)
            : "";
        Image = item.ImageUrl is { Scheme: "https" } url
            ? new UriImageSource { Uri = url, CachingEnabled = true, CacheValidity = TimeSpan.FromDays(30) }
            : null;
        IsRecent = item.Published is { } at && DateTimeOffset.UtcNow - at < RecentFor;
        IsRead = item.IsRead;
        OpenCommand = new AsyncRelayCommand(() => open(this));
    }

    public string Id { get; }

    public Uri? Link { get; }

    public string Title { get; }

    public string Summary { get; }

    public bool HasSummary => Summary.Length > 0;

    public string DateText { get; }

    public ImageSource? Image { get; }

    public bool HasImage => Image is not null;

    public bool IsRecent { get; }

    /// <summary>Opened before; recent unread articles carry a "New" badge.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNew))]
    public partial bool IsRead { get; set; }

    public bool IsNew => IsRecent && !IsRead;

    public IAsyncRelayCommand OpenCommand { get; }
}
