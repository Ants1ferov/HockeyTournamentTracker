namespace HockeyTournamentTracker.Domain;

public sealed class StatsService
{
    public IReadOnlyList<Standing> CalculateStandings(
        Tournament tournament,
        IReadOnlyList<Team> teams,
        IReadOnlyList<Match> matches)
    {
        var rules = tournament.Rules;

        var standings = teams.ToDictionary(
            t => t.Id,
            t => new Standing
            {
                TournamentId = tournament.Id,
                TeamId = t.Id
            });

        foreach (var match in matches.Where(m => m.Status == MatchStatus.Finished))
        {
            if (match.OutcomeType is null)
            {
                continue;
            }

            if (!standings.TryGetValue(match.HomeTeamId, out var homeStanding) ||
                !standings.TryGetValue(match.AwayTeamId, out var awayStanding))
            {
                continue;
            }

            var effectiveScore = MatchOutcomeResolver.GetEffectiveFinalScore(match);
            if (effectiveScore is null)
                continue;

            var homeGoals = effectiveScore.Value.HomeGoals;
            var awayGoals = effectiveScore.Value.AwayGoals;

            homeStanding.Games++;
            awayStanding.Games++;

            homeStanding.GoalsFor += homeGoals;
            homeStanding.GoalsAgainst += awayGoals;

            awayStanding.GoalsFor += awayGoals;
            awayStanding.GoalsAgainst += homeGoals;

            switch (match.OutcomeType)
            {
                case OutcomeType.Regulation:
                    ApplyRegulationOutcome(match, homeGoals, awayGoals, homeStanding, awayStanding, rules);
                    break;
                case OutcomeType.Overtime:
                    ApplyOvertimeOutcome(match, homeGoals, awayGoals, homeStanding, awayStanding, rules);
                    break;
                case OutcomeType.Shootout:
                    ApplyShootoutOutcome(match, homeStanding, awayStanding, rules);
                    break;
            }
        }

        var order = rules.SortOrder is { Count: > 0 } sortOrder
            ? sortOrder
            : TournamentRules.GetDefaultSortOrder();

        IOrderedEnumerable<Standing>? ordered = null;
        foreach (var criterion in order)
        {
            if (ordered is null)
                ordered = standings.Values.OrderByDescending(s => GetSortKey(s, criterion));
            else
                ordered = ordered.ThenByDescending(s => GetSortKey(s, criterion));
        }

        return (ordered ?? standings.Values.OrderByDescending(s => s.Points)).ToList();
    }

    /// <summary>
    /// Сортирует список standings по правилам турнира (для расчёта мест внутри группы).
    /// </summary>
    public static IReadOnlyList<Standing> SortByRules(IEnumerable<Standing> standings, TournamentRules rules)
    {
        var order = rules.SortOrder is { Count: > 0 } sortOrder
            ? sortOrder
            : TournamentRules.GetDefaultSortOrder();
        IOrderedEnumerable<Standing>? ordered = null;
        foreach (var criterion in order)
        {
            if (ordered is null)
                ordered = standings.OrderByDescending(s => GetSortKey(s, criterion));
            else
                ordered = ordered.ThenByDescending(s => GetSortKey(s, criterion));
        }
        return (ordered ?? standings.OrderByDescending(s => s.Points)).ToList();
    }

    private static int GetSortKey(Standing s, StandingSortCriterion criterion) => criterion switch
    {
        StandingSortCriterion.Points => s.Points,
        StandingSortCriterion.WinsRegulation => s.WinsRegulation,
        StandingSortCriterion.WinsOvertime => s.WinsOvertime,
        StandingSortCriterion.WinsShootout => s.WinsShootout,
        StandingSortCriterion.GoalDifference => s.GoalDifference,
        StandingSortCriterion.GoalsFor => s.GoalsFor,
        StandingSortCriterion.GoalsAgainst => s.GoalsAgainst,
        StandingSortCriterion.Games => s.Games,
        _ => s.Points
    };

    private static void ApplyRegulationOutcome(
        Match match,
        int homeGoals,
        int awayGoals,
        Standing home,
        Standing away,
        TournamentRules rules)
    {
        var outcomeResolved = TryResolveHomeWin(match, homeGoals, awayGoals, out var homeWon);
        if (!outcomeResolved)
            return;

        if (homeWon)
        {
            home.WinsRegulation++;
            away.LossesRegulation++;

            home.Points += rules.PointsForRegulationWin;
            away.Points += rules.PointsForRegulationLoss;
        }
        else
        {
            away.WinsRegulation++;
            home.LossesRegulation++;

            away.Points += rules.PointsForRegulationWin;
            home.Points += rules.PointsForRegulationLoss;
        }
    }

    private static void ApplyOvertimeOutcome(
        Match match,
        int homeGoals,
        int awayGoals,
        Standing home,
        Standing away,
        TournamentRules rules)
    {
        var outcomeResolved = TryResolveHomeWin(match, homeGoals, awayGoals, out var homeWon);
        if (!outcomeResolved)
            return;

        if (homeWon)
        {
            home.WinsOvertime++;
            away.LossesOvertime++;

            home.Points += rules.PointsForOvertimeWin;
            away.Points += rules.PointsForOvertimeLoss;
        }
        else
        {
            away.WinsOvertime++;
            home.LossesOvertime++;

            away.Points += rules.PointsForOvertimeWin;
            home.Points += rules.PointsForOvertimeLoss;
        }
    }

    private static void ApplyShootoutOutcome(
        Match match,
        Standing home,
        Standing away,
        TournamentRules rules)
    {
        if (!TryResolveHomeWin(match, match.HomeGoals ?? 0, match.AwayGoals ?? 0, out var homeWon))
            return;

        if (homeWon)
        {
            home.WinsShootout++;
            away.LossesShootout++;

            home.Points += rules.PointsForShootoutWin;
            away.Points += rules.PointsForShootoutLoss;
        }
        else
        {
            away.WinsShootout++;
            home.LossesShootout++;

            away.Points += rules.PointsForShootoutWin;
            home.Points += rules.PointsForShootoutLoss;
        }
    }

    private static bool TryResolveHomeWin(Match match, int homeGoals, int awayGoals, out bool homeWon)
    {
        if (homeGoals != awayGoals)
        {
            homeWon = homeGoals > awayGoals;
            return true;
        }

        if (!MatchOutcomeResolver.TryGetWinnerTeamId(match, out var winnerTeamId))
        {
            homeWon = false;
            return false;
        }

        homeWon = winnerTeamId == match.HomeTeamId;
        return true;
    }
}

