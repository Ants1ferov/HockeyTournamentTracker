namespace HockeyTournamentTracker.Domain;

public enum TournamentStatus
{
    Planned,
    InProgress,
    Finished,
    Archived
}

public enum MatchStatus
{
    Scheduled = 0,
    Finished = 1,
    Cancelled = 2,
    InProgress = 3
}

public enum OutcomeType
{
    Regulation,
    Overtime,
    Shootout
}

/// <summary>Тип периода матча для отображения (основной / ОТ / буллиты).</summary>
public enum PeriodType
{
    Regular,
    Overtime,
    Shootout
}

/// <summary>
/// Критерии сортировки турнирной таблицы (порядок распределения мест).
/// </summary>
public enum StandingSortCriterion
{
    Points,
    WinsRegulation,
    WinsOvertime,
    WinsShootout,
    GoalDifference,
    GoalsFor,
    GoalsAgainst,
    Games
}

