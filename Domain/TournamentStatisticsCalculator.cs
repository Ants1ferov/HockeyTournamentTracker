using System.Globalization;

namespace HockeyTournamentTracker.Domain;

/// <summary>
/// Статистика по турниру: периоды 1–3, голы с учётом ОТ/Б, места с разделением.
/// </summary>
public static class TournamentStatisticsCalculator
{
    public sealed record PeriodTeamRow(
        int Rank,
        Guid TeamId,
        string TeamName,
        int Wins,
        int Draws,
        int Losses);

    public sealed record GoalsTeamRow(int Rank, Guid TeamId, string TeamName, int Goals);

    public sealed record MonthlyPlaceRow(DateTime MonthLabel, int? Place, string PlaceDisplay, string MonthLabelText);

    /// <summary>Периоды 1–3: только основные периоды, без ОТ/Б. Завершённые матчи.</summary>
    public static IReadOnlyList<PeriodTeamRow> BuildPeriodTable(
        IReadOnlyList<Team> teams,
        IReadOnlyList<Match> finishedMatches,
        int periodNumber)
    {
        var teamById = teams.ToDictionary(t => t.Id);
        var acc = teams.ToDictionary(t => t.Id, _ => (W: 0, D: 0, L: 0));

        foreach (var m in finishedMatches.Where(x => x.Status == MatchStatus.Finished && x.OutcomeType.HasValue))
        {
            if (!teamById.ContainsKey(m.HomeTeamId) || !teamById.ContainsKey(m.AwayTeamId))
                continue;

            var p = m.PeriodScores?.FirstOrDefault(x =>
                x.PeriodType == PeriodType.Regular && x.PeriodNumber == periodNumber);
            if (p is null)
                continue;

            ApplyPeriodOutcome(m.HomeTeamId, m.AwayTeamId, p.HomeGoals, p.AwayGoals, acc);
        }

        var sorted = acc
            .Select(kv =>
            {
                teamById.TryGetValue(kv.Key, out var team);
                var name = string.IsNullOrWhiteSpace(team?.Name) ? "—" : team!.Name;
                return (TeamId: kv.Key, Name: name, kv.Value.W, kv.Value.D, kv.Value.L);
            })
            .OrderByDescending(x => x.W)
            .ThenBy(x => x.Name, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true))
            .ToList();

        return ApplyCompetitiveRank(
            sorted,
            x => x.W,
            (rank, x) => new PeriodTeamRow(rank, x.TeamId, x.Name, x.W, x.D, x.L));
    }

    private static void ApplyPeriodOutcome(
        Guid homeId,
        Guid awayId,
        int homeGoals,
        int awayGoals,
        Dictionary<Guid, (int W, int D, int L)> acc)
    {
        void add(Guid id, int w, int d, int l)
        {
            var t = acc[id];
            acc[id] = (t.W + w, t.D + d, t.L + l);
        }

        if (homeGoals > awayGoals)
        {
            add(homeId, 1, 0, 0);
            add(awayId, 0, 0, 1);
        }
        else if (homeGoals < awayGoals)
        {
            add(homeId, 0, 0, 1);
            add(awayId, 1, 0, 0);
        }
        else
        {
            add(homeId, 0, 1, 0);
            add(awayId, 0, 1, 0);
        }
    }

    public static IReadOnlyList<GoalsTeamRow> BuildGoalsForTable(
        IReadOnlyList<Team> teams,
        IReadOnlyList<Match> finishedMatches)
    {
        var teamById = teams.ToDictionary(t => t.Id);
        var goals = teams.ToDictionary(t => t.Id, _ => 0);

        foreach (var m in finishedMatches.Where(x => x.Status == MatchStatus.Finished && x.OutcomeType.HasValue))
        {
            if (!teamById.ContainsKey(m.HomeTeamId) || !teamById.ContainsKey(m.AwayTeamId))
                continue;

            var eff = MatchOutcomeResolver.GetEffectiveFinalScore(m);
            if (eff is null)
                continue;

            goals[m.HomeTeamId] += eff.Value.HomeGoals;
            goals[m.AwayTeamId] += eff.Value.AwayGoals;
        }

        var sorted = goals
            .Select(kv =>
            {
                teamById.TryGetValue(kv.Key, out var team);
                var name = string.IsNullOrWhiteSpace(team?.Name) ? "—" : team!.Name;
                return (TeamId: kv.Key, Name: name, G: kv.Value);
            })
            .OrderByDescending(x => x.G)
            .ThenBy(x => x.Name, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true))
            .ToList();

        return ApplyCompetitiveRank(
            sorted,
            x => x.G,
            (rank, x) => new GoalsTeamRow(rank, x.TeamId, x.Name, x.G));
    }

    public static IReadOnlyList<GoalsTeamRow> BuildGoalsAgainstTable(
        IReadOnlyList<Team> teams,
        IReadOnlyList<Match> finishedMatches)
    {
        var teamById = teams.ToDictionary(t => t.Id);
        var against = teams.ToDictionary(t => t.Id, _ => 0);

        foreach (var m in finishedMatches.Where(x => x.Status == MatchStatus.Finished && x.OutcomeType.HasValue))
        {
            if (!teamById.ContainsKey(m.HomeTeamId) || !teamById.ContainsKey(m.AwayTeamId))
                continue;

            var eff = MatchOutcomeResolver.GetEffectiveFinalScore(m);
            if (eff is null)
                continue;

            against[m.HomeTeamId] += eff.Value.AwayGoals;
            against[m.AwayTeamId] += eff.Value.HomeGoals;
        }

        var sorted = against
            .Select(kv =>
            {
                teamById.TryGetValue(kv.Key, out var team);
                var name = string.IsNullOrWhiteSpace(team?.Name) ? "—" : team!.Name;
                return (TeamId: kv.Key, Name: name, G: kv.Value);
            })
            .OrderBy(x => x.G)
            .ThenBy(x => x.Name, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true))
            .ToList();

        return ApplyCompetitiveRank(
            sorted,
            x => x.G,
            (rank, x) => new GoalsTeamRow(rank, x.TeamId, x.Name, x.G));
    }

    public static IReadOnlyList<MonthlyPlaceRow> BuildMonthlyStandingsPlaces(
        Tournament tournament,
        IReadOnlyList<Team> teamsInStage,
        IReadOnlyList<Match> stageMatches,
        Guid teamId)
    {
        DateTime? firstMatchMonth = null;
        var matchDates = stageMatches
            .Where(m => m.DateTime.HasValue)
            .Select(m => m.DateTime!.Value.Date)
            .ToList();
        if (matchDates.Count > 0)
            firstMatchMonth = new DateTime(matchDates.Min().Year, matchDates.Min().Month, 1);

        var start = tournament.StartDate is { } sd
            ? new DateTime(sd.Year, sd.Month, 1)
            : firstMatchMonth ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        var endMonth = DateTime.Today;
        if (tournament.EndDate is { } te && te.Date < endMonth)
            endMonth = te.Date;

        var months = new List<DateTime>();
        var cursor = new DateTime(start.Year, start.Month, 1);
        var last = new DateTime(endMonth.Year, endMonth.Month, 1);
        while (cursor <= last)
        {
            months.Add(cursor);
            cursor = cursor.AddMonths(1);
        }

        var statsService = new StatsService();
        var rules = tournament.Rules ?? new TournamentRules();
        var result = new List<MonthlyPlaceRow>();
        var ru = CultureInfo.GetCultureInfo("ru-RU");

        foreach (var monthStart in months)
        {
            var lastDay = new DateTime(monthStart.Year, monthStart.Month,
                DateTime.DaysInMonth(monthStart.Year, monthStart.Month));
            var monthText = lastDay.ToString("MMMM yyyy", ru);
            if (monthText.Length > 0)
                monthText = char.ToUpper(monthText[0], ru) + monthText[1..];

            var cutoff = lastDay.Date.AddDays(1).AddTicks(-1);

            var matchesUpTo = stageMatches
                .Where(m =>
                    m.Status == MatchStatus.Finished &&
                    m.OutcomeType.HasValue &&
                    (m.DateTime ?? DateTime.MinValue) <= cutoff)
                .ToList();

            if (matchesUpTo.Count == 0)
            {
                result.Add(new MonthlyPlaceRow(lastDay, null, "—", monthText));
                continue;
            }

            var standings = statsService.CalculateStandings(tournament, teamsInStage, matchesUpTo);
            var sorted = StatsService.SortByRules(standings, rules).ToList();

            var idx = sorted.FindIndex(s => s.TeamId == teamId);
            if (idx < 0)
            {
                result.Add(new MonthlyPlaceRow(lastDay, null, "—", monthText));
                continue;
            }

            var place = ComputeCompetitiveRank(sorted, idx, rules);
            result.Add(new MonthlyPlaceRow(lastDay, place, place.ToString(), monthText));
        }

        return result;
    }

    private static int ComputeCompetitiveRank(IReadOnlyList<Standing> sorted, int index, TournamentRules rules)
    {
        if (sorted.Count == 0 || index < 0 || index >= sorted.Count)
            return 0;
        var ranks = new int[sorted.Count];
        ranks[0] = 1;
        for (var i = 1; i < sorted.Count; i++)
        {
            ranks[i] = StandingsEqual(sorted[i], sorted[i - 1], rules) ? ranks[i - 1] : i + 1;
        }
        return ranks[index];
    }

    private static bool StandingsEqual(Standing a, Standing b, TournamentRules rules)
    {
        var order = rules.SortOrder is { Count: > 0 } so ? so : TournamentRules.GetDefaultSortOrder();
        foreach (var c in order)
        {
            if (GetKey(a, c) != GetKey(b, c))
                return false;
        }
        return true;
    }

    private static int GetKey(Standing s, StandingSortCriterion c) => c switch
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

    private static List<TOut> ApplyCompetitiveRank<TItem, TOut>(
        List<TItem> sorted,
        Func<TItem, int> keySelector,
        Func<int, TItem, TOut> factory)
    {
        var result = new List<TOut>();
        if (sorted.Count == 0)
            return result;

        var rank = 1;
        for (var i = 0; i < sorted.Count; i++)
        {
            if (i > 0 && keySelector(sorted[i]) != keySelector(sorted[i - 1]))
                rank = i + 1;
            result.Add(factory(rank, sorted[i]));
        }
        return result;
    }
}
