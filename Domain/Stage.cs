namespace HockeyTournamentTracker.Domain;

public sealed class Stage
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public StageType StageType { get; set; } = StageType.Swiss;
}
