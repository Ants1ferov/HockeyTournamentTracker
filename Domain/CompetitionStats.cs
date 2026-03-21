namespace HockeyTournamentTracker.Domain;

public sealed class CompetitionStats
{
    public int FinishedMatches { get; set; }

    public int WinsRegulation { get; set; }
    public int WinsOvertime { get; set; }
    public int WinsShootout { get; set; }

    public int LossesRegulation { get; set; }
    public int LossesOvertime { get; set; }
    public int LossesShootout { get; set; }

    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }

    public PeriodAggregate Period1 { get; set; } = new();
    public PeriodAggregate Period2 { get; set; } = new();
    public PeriodAggregate Period3 { get; set; } = new();
    public PeriodAggregate Overtime { get; set; } = new();
    public PeriodAggregate Shootout { get; set; } = new();
}

public sealed class PeriodAggregate
{
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
}

public sealed class HeadToHeadStats
{
    public int Matches { get; set; }
    public int TeamAWins { get; set; }
    public int TeamBWins { get; set; }
    public int TeamAGoals { get; set; }
    public int TeamBGoals { get; set; }
}
