namespace HockeyTournamentTracker.Domain;

public sealed class PeriodScore
{
    public int PeriodNumber { get; set; }
    public PeriodType PeriodType { get; set; }
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
}
