using HockeyTournamentTracker.Presentation.Views;

namespace HockeyTournamentTracker;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(TournamentEditPage), typeof(TournamentEditPage));
		Routing.RegisterRoute(nameof(TournamentDetailsPage), typeof(TournamentDetailsPage));
		Routing.RegisterRoute(nameof(MatchEditPage), typeof(MatchEditPage));
	}
}
