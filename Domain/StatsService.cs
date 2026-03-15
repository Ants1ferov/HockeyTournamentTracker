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
            if (match.HomeGoals is null || match.AwayGoals is null || match.OutcomeType is null)
            {
                continue;
            }

            if (!standings.TryGetValue(match.HomeTeamId, out var homeStanding) ||
                !standings.TryGetValue(match.AwayTeamId, out var awayStanding))
            {
                continue;
            }

            homeStanding.Games++;
            awayStanding.Games++;

            homeStanding.GoalsFor += match.HomeGoals.Value;
            homeStanding.GoalsAgainst += match.AwayGoals.Value;

            awayStanding.GoalsFor += match.AwayGoals.Value;
            awayStanding.GoalsAgainst += match.HomeGoals.Value;

            switch (match.OutcomeType)
            {
                case OutcomeType.Regulation:
                    ApplyRegulationOutcome(match, homeStanding, awayStanding, rules);
                    break;
                case OutcomeType.Overtime:
                    ApplyOvertimeOutcome(match, homeStanding, awayStanding, rules);
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
        Standing home,
        Standing away,
        TournamentRules rules)
    {
        if (match.HomeGoals > match.AwayGoals)
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
        Standing home,
        Standing away,
        TournamentRules rules)
    {
        if (match.HomeGoals > match.AwayGoals)
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
        if (match.HomeGoals > match.AwayGoals)
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
}

