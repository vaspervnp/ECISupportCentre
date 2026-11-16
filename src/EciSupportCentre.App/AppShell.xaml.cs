using EciSupportCentre.App.ViewModels;
using EciSupportCentre.App.Views;

namespace EciSupportCentre.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(AppRoutes.InitiativeRoute, typeof(InitiativeDetailPage));
        Routing.RegisterRoute(AppRoutes.AboutRoute, typeof(AboutPage));
    }
}
