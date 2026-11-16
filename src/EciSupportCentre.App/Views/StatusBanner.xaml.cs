using EciSupportCentre.App.ViewModels;

namespace EciSupportCentre.App.Views;

/// <summary>Shared offline / sync-error banner; every instance shows the same <see cref="SyncStatusViewModel"/>.</summary>
public partial class StatusBanner : ContentView
{
    public StatusBanner()
    {
        BindingContext = IPlatformApplication.Current?.Services.GetService<SyncStatusViewModel>();
        InitializeComponent();
    }
}
