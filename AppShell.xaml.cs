using HockeyTournamentTracker.Presentation.Views;

namespace HockeyTournamentTracker;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(TournamentEditPage), typeof(TournamentEditPage));
		Routing.RegisterRoute(nameof(TournamentDetailsPage), typeof(TournamentDetailsPage));
		Routing.RegisterRoute(nameof(TeamsListPage), typeof(TeamsListPage));
		Routing.RegisterRoute(nameof(TeamEditPage), typeof(TeamEditPage));
		Routing.RegisterRoute(nameof(MatchEditPage), typeof(MatchEditPage));
		Routing.RegisterRoute(nameof(TournamentRulesEditPage), typeof(TournamentRulesEditPage));
		Routing.RegisterRoute(nameof(GroupsListPage), typeof(GroupsListPage));
		Routing.RegisterRoute(nameof(StageEditPage), typeof(StageEditPage));
	}
}
