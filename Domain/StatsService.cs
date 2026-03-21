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

    public CompetitionStats CalculateCompetitionStats(IReadOnlyList<Match> matches)
    {
        var stats = new CompetitionStats();

        foreach (var match in matches.Where(m => m.Status == MatchStatus.Finished))
        {
            if (!TryBuildStatRecord(match, out var record))
                continue;

            stats.FinishedMatches++;
            stats.GoalsFor += record.HomeGoals + record.AwayGoals;
            stats.GoalsAgainst += record.HomeGoals + record.AwayGoals;

            switch (record.OutcomeType)
            {
                case OutcomeType.Regulation:
                    stats.WinsRegulation++;
                    stats.LossesRegulation++;
                    break;
                case OutcomeType.Overtime:
                    stats.WinsOvertime++;
                    stats.LossesOvertime++;
                    break;
                case OutcomeType.Shootout:
                    stats.WinsShootout++;
                    stats.LossesShootout++;
                    break;
            }

            TryFillPeriodStats(record.Periods, 1, stats.Period1);
            TryFillPeriodStats(record.Periods, 2, stats.Period2);
            TryFillPeriodStats(record.Periods, 3, stats.Period3);
            TryFillSpecialPeriodStats(record.Periods, PeriodType.Overtime, stats.Overtime);
            TryFillSpecialPeriodStats(record.Periods, PeriodType.Shootout, stats.Shootout);
        }

        return stats;
    }

    public HeadToHeadStats CalculateHeadToHead(
        IReadOnlyList<Match> matches,
        Guid teamAId,
        Guid teamBId)
    {
        var stats = new HeadToHeadStats();

        foreach (var match in matches.Where(m =>
                     m.Status == MatchStatus.Finished &&
                     ((m.HomeTeamId == teamAId && m.AwayTeamId == teamBId) ||
                      (m.HomeTeamId == teamBId && m.AwayTeamId == teamAId))))
        {
            if (!TryBuildStatRecord(match, out var record))
                continue;

            stats.Matches++;
            var aIsHome = match.HomeTeamId == teamAId;
            var goalsA = aIsHome ? record.HomeGoals : record.AwayGoals;
            var goalsB = aIsHome ? record.AwayGoals : record.HomeGoals;
            stats.TeamAGoals += goalsA;
            stats.TeamBGoals += goalsB;

            if (goalsA > goalsB)
                stats.TeamAWins++;
            else if (goalsA < goalsB)
                stats.TeamBWins++;
        }

        return stats;
    }

    private static void TryFillPeriodStats(IReadOnlyList<PeriodScore> periods, int periodNumber, PeriodAggregate target)
    {
        var period = periods.FirstOrDefault(p => p.PeriodType == PeriodType.Regular && p.PeriodNumber == periodNumber);
        if (period is null)
            return;

        if (period.HomeGoals > period.AwayGoals)
            target.Wins++;
        else if (period.HomeGoals < period.AwayGoals)
            target.Losses++;
        else
            target.Draws++;
    }

    private static void TryFillSpecialPeriodStats(IReadOnlyList<PeriodScore> periods, PeriodType periodType, PeriodAggregate target)
    {
        var period = periods.FirstOrDefault(p => p.PeriodType == periodType);
        if (period is null)
            return;

        if (period.HomeGoals > period.AwayGoals)
            target.Wins++;
        else if (period.HomeGoals < period.AwayGoals)
            target.Losses++;
        else
            target.Draws++;
    }

    private static bool TryBuildStatRecord(Match match, out MatchStatRecord record)
    {
        record = default;
        if (match.OutcomeType is null)
            return false;

        var effective = MatchOutcomeResolver.GetEffectiveFinalScore(match);
        if (effective is null)
            return false;

        var periods = match.PeriodScores is { Count: > 0 }
            ? match.PeriodScores.ToList()
            : new List<PeriodScore>
            {
                new()
                {
                    PeriodNumber = 1,
                    PeriodType = PeriodType.Regular,
                    HomeGoals = effective.Value.HomeGoals,
                    AwayGoals = effective.Value.AwayGoals
                }
            };

        record = new MatchStatRecord(
            effective.Value.HomeGoals,
            effective.Value.AwayGoals,
            effective.Value.HomeGoals > effective.Value.AwayGoals,
            match.OutcomeType.Value,
            periods);
        return true;
    }

    private readonly record struct MatchStatRecord(
        int HomeGoals,
        int AwayGoals,
        bool HomeWon,
        OutcomeType OutcomeType,
        IReadOnlyList<PeriodScore> Periods);
}

