using EciSupportCentre.App.ViewModels;

namespace EciSupportCentre.App.Views;

public partial class NewsPage : ContentPage
{
    private readonly NewsViewModel _viewModel;

    public NewsPage(NewsViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();

        // Two columns of articles on tablets, as wide as the Initiatives split layout.
        SizeChanged += (_, _) =>
        {
            if (List.ItemsLayout is GridItemsLayout grid)
            {
                var span = Width >= InitiativesPage.WideLayoutWidth ? 2 : 1;
                if (grid.Span != span)
                {
                    grid.Span = span;
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }
}
