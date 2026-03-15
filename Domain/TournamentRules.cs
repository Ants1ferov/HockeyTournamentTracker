namespace HockeyTournamentTracker.Domain;

public sealed class TournamentRules
{
    public int PointsForRegulationWin { get; init; } = 3;
    public int PointsForOvertimeWin { get; init; } = 2;
    public int PointsForShootoutWin { get; init; } = 2;

    public int PointsForRegulationLoss { get; init; } = 0;
    public int PointsForOvertimeLoss { get; init; } = 1;
    public int PointsForShootoutLoss { get; init; } = 1;

    /// <summary>
    /// Порядок критериев сортировки турнирной таблицы (распределение мест).
    /// Если пусто или null — используется порядок по умолчанию.
    /// </summary>
    public IReadOnlyList<StandingSortCriterion> SortOrder { get; init; } = GetDefaultSortOrder();

    /// <summary>Группы/конференции турнира (например Запад, Восток).</summary>
    public IReadOnlyList<GroupInfo> Groups { get; init; } = new List<GroupInfo>();

    /// <summary>Разрешать матчи между командами из разных групп.</summary>
    public bool AllowCrossGroupMatches { get; init; } = true;

    public static IReadOnlyList<StandingSortCriterion> GetDefaultSortOrder() => new[]
    {
        StandingSortCriterion.Points,
        StandingSortCriterion.WinsRegulation,
        StandingSortCriterion.GoalDifference,
        StandingSortCriterion.GoalsFor
    };
}

