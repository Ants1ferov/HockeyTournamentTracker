namespace HockeyTournamentTracker.Domain;

public sealed class GroupInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
}
