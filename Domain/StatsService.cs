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

        return standings.Values
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.WinsRegulation)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
            .ToList();
    }

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

