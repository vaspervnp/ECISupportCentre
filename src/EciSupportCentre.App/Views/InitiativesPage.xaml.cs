using EciSupportCentre.App.ViewModels;

namespace EciSupportCentre.App.Views;

public partial class InitiativesPage : ContentPage
{
    /// <summary>From this width (device-independent units) the list and the details are shown side by side.</summary>
    public const double WideLayoutWidth = 840;

    private readonly InitiativesViewModel _viewModel;

    public InitiativesPage(InitiativesViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
        SizeChanged += (_, _) => UpdateLayout();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }

    private bool? _appliedWide;

    private void UpdateLayout()
    {
        if (Width <= 0)
        {
            return;
        }

        var wide = Width >= WideLayoutWidth;
        if (wide == _appliedWide)
        {
            return;
        }

        _appliedWide = wide;
        _viewModel.IsWide = wide;

        // Change the widths of the two existing columns; replacing the definitions does not relayout.
        Root.ColumnDefinitions[0].Width = wide ? new GridLength(2, GridUnitType.Star) : GridLength.Star;
        Root.ColumnDefinitions[1].Width = wide ? new GridLength(3, GridUnitType.Star) : new GridLength(0);
        DetailPane.IsVisible = wide;
    }
}
