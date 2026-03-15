namespace HockeyTournamentTracker.Domain;

public sealed class Standing
{
    public Guid TournamentId { get; set; }
    public Guid TeamId { get; set; }

    public int Games { get; set; }

    public int WinsRegulation { get; set; }
    public int WinsOvertime { get; set; }
    public int WinsShootout { get; set; }

    public int LossesRegulation { get; set; }
    public int LossesOvertime { get; set; }
    public int LossesShootout { get; set; }

    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }

    public int GoalDifference => GoalsFor - GoalsAgainst;

    public int Points { get; set; }
}

