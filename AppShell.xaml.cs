using HockeyTournamentTracker.Presentation.Views;

namespace HockeyTournamentTracker;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(LeagueDetailsPage), typeof(LeagueDetailsPage));
		Routing.RegisterRoute(nameof(LeagueEditPage), typeof(LeagueEditPage));
		Routing.RegisterRoute(nameof(TournamentEditPage), typeof(TournamentEditPage));
		Routing.RegisterRoute(nameof(TournamentDetailsPage), typeof(TournamentDetailsPage));
		Routing.RegisterRoute(nameof(TeamsListPage), typeof(TeamsListPage));
		Routing.RegisterRoute(nameof(TeamEditPage), typeof(TeamEditPage));
		Routing.RegisterRoute(nameof(MatchEditPage), typeof(MatchEditPage));
		Routing.RegisterRoute(nameof(TournamentRulesEditPage), typeof(TournamentRulesEditPage));
		Routing.RegisterRoute(nameof(GroupsListPage), typeof(GroupsListPage));
		Routing.RegisterRoute(nameof(StageEditPage), typeof(StageEditPage));
		Routing.RegisterRoute(nameof(StageDetailsPage), typeof(StageDetailsPage));
		Routing.RegisterRoute(nameof(StageRosterPage), typeof(StageRosterPage));
		Routing.RegisterRoute(nameof(StageMatchesPage), typeof(StageMatchesPage));
		Routing.RegisterRoute(nameof(StageZonesEditPage), typeof(StageZonesEditPage));
		Routing.RegisterRoute(nameof(PlayoffBracketPage), typeof(PlayoffBracketPage));
		Routing.RegisterRoute(nameof(TournamentStatisticsPage), typeof(TournamentStatisticsPage));
	}
}
