using Microsoft.Extensions.DependencyInjection;

namespace EciSupportCentre.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		UserAppTheme = Services.AppSettings.Theme;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}