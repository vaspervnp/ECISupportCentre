using EciSupportCentre.App.ViewModels;

namespace EciSupportCentre.App.Views;

/// <summary>Opened from the Settings tab with the Shell route <c>about</c>.</summary>
public partial class AboutPage : ContentPage
{
    public AboutPage(AboutViewModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
    }
}
