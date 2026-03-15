namespace HockeyTournamentTracker.Domain;

public sealed class TournamentRules
{
    public int PointsForRegulationWin { get; init; } = 3;
    public int PointsForOvertimeWin { get; init; } = 2;
    public int PointsForShootoutWin { get; init; } = 2;

    public int PointsForRegulationLoss { get; init; } = 0;
    public int PointsForOvertimeLoss { get; init; } = 1;
    public int PointsForShootoutLoss { get; init; } = 1;

    // Порядок сортировки можно будет расширить позже при необходимости
}

