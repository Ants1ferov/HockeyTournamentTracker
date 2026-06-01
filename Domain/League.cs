namespace HockeyTournamentTracker.Domain;

public sealed class League
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconPath { get; set; }
    public int Order { get; set; }
    /// <summary>Вид спорта лиги (например «хоккей»). Необязательно.</summary>
    public string? Sport { get; set; }
}
