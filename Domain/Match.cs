namespace HockeyTournamentTracker.Domain;

public sealed class Match
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }

    public DateTime? DateTime { get; set; }

    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }

    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }

    public OutcomeType? OutcomeType { get; set; }
    public Guid? WinnerTeamId { get; set; }
    public Guid? LoserTeamId { get; set; }

    public int? ShootoutScoreHome { get; set; }
    public int? ShootoutScoreAway { get; set; }

    public MatchStatus Status { get; set; } = MatchStatus.Scheduled;

    public string? Notes { get; set; }

    /// <summary>Счёт по периодам (1–3 основные, 4 ОТ, 5 буллиты).</summary>
    public List<PeriodScore> PeriodScores { get; set; } = new();
}

