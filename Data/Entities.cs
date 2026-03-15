using SQLite;

namespace HockeyTournamentTracker.Data;

[Table("Tournaments")]
public class TournamentEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public int Status { get; set; }

    // Храним правила в JSON, чтобы не усложнять схему
    public string? RulesJson { get; set; }
}

[Table("Teams")]
public class TeamEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid TournamentId { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public string? ShortName { get; set; }
    public string? ColorHex { get; set; }
    public string? Notes { get; set; }
}

[Table("Matches")]
public class MatchEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid TournamentId { get; set; }

    public DateTime? DateTime { get; set; }

    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }

    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }

    public int? OutcomeType { get; set; }
    public Guid? WinnerTeamId { get; set; }
    public Guid? LoserTeamId { get; set; }

    public int? ShootoutScoreHome { get; set; }
    public int? ShootoutScoreAway { get; set; }

    public int Status { get; set; }

    public string? Notes { get; set; }
}

