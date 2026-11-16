using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EciSupportCentre.App.Localization;
using EciSupportCentre.Core.Storage;

namespace EciSupportCentre.App.ViewModels;

/// <summary>One initiative card in a list.</summary>
public sealed partial class InitiativeItemViewModel : ObservableObject
{
    public InitiativeItemViewModel(InitiativeListItem item, Func<InitiativeItemViewModel, Task> open)
    {
        Item = item;
        Title = item.Title;
        PhaseText = Loc.Phase(item.Phase);
        PhaseColor = Presentation.PhaseColor(item.Phase);
        SupportersText = Presentation.Supporters(item.TotalSupporters);
        DateText = Presentation.DateLine(item);
        Logo = Presentation.Logo(item.LogoId);
        OpenCommand = new AsyncRelayCommand(() => open(this));
    }

    public InitiativeListItem Item { get; }

    public long Id => Item.Id;

    public string Title { get; }

    public string PhaseText { get; }

    public Color PhaseColor { get; }

    public string SupportersText { get; }

    public string DateText { get; }

    public bool HasDateText => DateText.Length > 0;

    public ImageSource? Logo { get; }

    public bool HasLogo => Logo is not null;

    public bool LastCall => Item.LastCall && Item.Phase == Core.Models.InitiativePhase.Collecting;

    public bool IsFollowed => Item.IsFollowed;

    public bool IsSigned => Item.IsSigned;

    public IAsyncRelayCommand OpenCommand { get; }

    /// <summary>Highlighted in the tablet layout while its details are shown.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
