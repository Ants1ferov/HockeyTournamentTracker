namespace HockeyTournamentTracker.Domain;

public sealed class PlayoffSettings
{
    public Guid StageId { get; set; }
    public bool UseReseeding { get; set; }
    public bool HasThirdPlaceMatch { get; set; }
}

public sealed class PlayoffRound
{
    public Guid Id { get; set; }
    public Guid StageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public int DefaultBestOf { get; set; } = 1;
}

public sealed class PlayoffSeries
{
    public Guid Id { get; set; }
    public Guid StageId { get; set; }
    public Guid RoundId { get; set; }
    public int Slot { get; set; }
    public Guid? HomeTeamId { get; set; }
    public Guid? AwayTeamId { get; set; }
    public int? HomeSeed { get; set; }
    public int? AwaySeed { get; set; }
    public int? BestOfOverride { get; set; }
    public Guid? WinnerTeamId { get; set; }
    public bool IsThirdPlace { get; set; }
}
