using EciSupportCentre.App.Services;
using EciSupportCentre.App.ViewModels;

namespace EciSupportCentre.App.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        NotificationLaunch.OpenPending();
        await _viewModel.OnAppearingAsync();
    }
}
