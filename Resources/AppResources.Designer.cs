using System.Resources;

namespace HockeyTournamentTracker.Resources;

public static class AppResources
{
    private static readonly ResourceManager ResourceManager =
        new("HockeyTournamentTracker.Resources.AppResources", typeof(AppResources).Assembly);

    public static string AppTitle => GetString(nameof(AppTitle));
    public static string TabTournaments => GetString(nameof(TabTournaments));
    public static string MyTournaments => GetString(nameof(MyTournaments));
    public static string Add => GetString(nameof(Add));
    public static string Save => GetString(nameof(Save));
    public static string Cancel => GetString(nameof(Cancel));
    public static string Ok => GetString(nameof(Ok));
    public static string Delete => GetString(nameof(Delete));
    public static string Teams => GetString(nameof(Teams));
    public static string AddTeam => GetString(nameof(AddTeam));
    public static string NewTournament => GetString(nameof(NewTournament));
    public static string TournamentName => GetString(nameof(TournamentName));
    public static string TournamentNamePlaceholder => GetString(nameof(TournamentNamePlaceholder));
    public static string Description => GetString(nameof(Description));
    public static string StartDate => GetString(nameof(StartDate));
    public static string EndDate => GetString(nameof(EndDate));
    public static string StandingsTable => GetString(nameof(StandingsTable));
    public static string Matches => GetString(nameof(Matches));
    public static string AddMatch => GetString(nameof(AddMatch));
    public static string NewMatch => GetString(nameof(NewMatch));
    public static string HomeTeam => GetString(nameof(HomeTeam));
    public static string AwayTeam => GetString(nameof(AwayTeam));
    public static string Date => GetString(nameof(Date));
    public static string Time => GetString(nameof(Time));
    public static string Score => GetString(nameof(Score));
    public static string HowMatchEnded => GetString(nameof(HowMatchEnded));
    public static string OutcomeRegulation => GetString(nameof(OutcomeRegulation));
    public static string OutcomeOvertime => GetString(nameof(OutcomeOvertime));
    public static string OutcomeShootout => GetString(nameof(OutcomeShootout));
    public static string ShootoutScore => GetString(nameof(ShootoutScore));
    public static string Error => GetString(nameof(Error));
    public static string ErrorCheckTeamsAndScore => GetString(nameof(ErrorCheckTeamsAndScore));
    public static string DeleteTournamentConfirm => GetString(nameof(DeleteTournamentConfirm));
    public static string Status => GetString(nameof(Status));
    public static string StatusPlanned => GetString(nameof(StatusPlanned));
    public static string StatusInProgress => GetString(nameof(StatusInProgress));
    public static string StatusFinished => GetString(nameof(StatusFinished));
    public static string StatusArchived => GetString(nameof(StatusArchived));
    public static string Live => GetString(nameof(Live));
    public static string LiveMatches => GetString(nameof(LiveMatches));
    public static string StartMatch => GetString(nameof(StartMatch));
    public static string FinishMatch => GetString(nameof(FinishMatch));
    public static string SelectIcon => GetString(nameof(SelectIcon));
    public static string TeamName => GetString(nameof(TeamName));
    public static string ShortName => GetString(nameof(ShortName));
    public static string NewTeam => GetString(nameof(NewTeam));
    public static string EditTeam => GetString(nameof(EditTeam));
    public static string DeleteTeamConfirm => GetString(nameof(DeleteTeamConfirm));

    public static string GetString(string key) =>
        ResourceManager.GetString(key) ?? key;
}
