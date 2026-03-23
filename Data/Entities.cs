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
    public string? IconPath { get; set; }
    public string? Notes { get; set; }

    public Guid? GroupId { get; set; }
}

[Table("Stages")]
public class StageEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid TournamentId { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }
    public int StageType { get; set; }
}

[Table("StageTeams")]
public class StageTeamEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid StageId { get; set; }

    [Indexed]
    public Guid TeamId { get; set; }

    /// <summary>
    /// Группа команды внутри конкретной стадии.
    /// Если null — команда считается "Без группы" в этой стадии.
    /// </summary>
    public Guid? GroupId { get; set; }
}

[Table("StageGroups")]
public class StageGroupEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid StageId { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }
}

[Table("Matches")]
public class MatchEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid TournamentId { get; set; }

    public Guid? StageId { get; set; }
    [Indexed]
    public Guid? SeriesId { get; set; }

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

    public string? PeriodScoresJson { get; set; }
}

[Table("PlayoffSettings")]
public class PlayoffSettingsEntity
{
    [PrimaryKey]
    public Guid StageId { get; set; }
    public int UseReseeding { get; set; }
    public int HasThirdPlaceMatch { get; set; }
}

[Table("PlayoffRounds")]
public class PlayoffRoundEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }
    [Indexed]
    public Guid StageId { get; set; }
    [NotNull]
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public int DefaultBestOf { get; set; }
}

[Table("StageColorZones")]
public class StageColorZoneEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid StageId { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    [NotNull]
    public string ColorHex { get; set; } = "#808080";

    public int SortOrder { get; set; }
}

/// <summary>Привязка команды к цветовой зоне в рамках стадии (не более одной зоны на команду).</summary>
[Table("StageTeamZones")]
public class StageTeamZoneEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid StageId { get; set; }

    [Indexed]
    public Guid TeamId { get; set; }

    public Guid ZoneId { get; set; }
}

[Table("PlayoffSeries")]
public class PlayoffSeriesEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }
    [Indexed]
    public Guid StageId { get; set; }
    [Indexed]
    public Guid RoundId { get; set; }
    public int Slot { get; set; }
    public Guid? HomeTeamId { get; set; }
    public Guid? AwayTeamId { get; set; }
    public int? HomeSeed { get; set; }
    public int? AwaySeed { get; set; }
    public int? BestOfOverride { get; set; }
    public Guid? WinnerTeamId { get; set; }
    public int IsThirdPlace { get; set; }
}

