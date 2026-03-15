using HockeyTournamentTracker.Data;

namespace HockeyTournamentTracker;

public partial class App : Application
{
	public App(LocalDatabase database)
	{
		InitializeComponent();
		_ = database.InitializeAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}