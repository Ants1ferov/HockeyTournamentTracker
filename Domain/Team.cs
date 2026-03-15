namespace HockeyTournamentTracker.Domain;

public sealed class Team
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }

    public string? ColorHex { get; set; }
    public string? IconPath { get; set; }
    public string? Notes { get; set; }

    /// <summary>Группа/конференция (если турнир использует группы).</summary>
    public Guid? GroupId { get; set; }
}

