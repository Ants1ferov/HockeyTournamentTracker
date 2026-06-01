namespace HockeyTournamentTracker.Domain;

public sealed class Tournament
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public TournamentStatus Status { get; set; } = TournamentStatus.Planned;

    public TournamentRules Rules { get; set; } = new();

    public Guid? LeagueId { get; set; }
}

