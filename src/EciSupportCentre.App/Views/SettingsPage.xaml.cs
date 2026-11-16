using EciSupportCentre.App.ViewModels;

namespace EciSupportCentre.App.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
    }
}
