using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

/// <summary>
/// Единая фабрика строк матча (<see cref="MatchRow"/>). Централизует построение
/// счёта/периодов, чтобы логика не дублировалась по нескольким VM/страницам.
/// </summary>
internal static class MatchRowFactory
{
    public static MatchRow Create(
        Match m,
        string? homeName,
        string? awayName,
        string? homeIcon,
        string? awayIcon,
        bool isLive) => new()
    {
        MatchId = m.Id,
        SeriesId = m.SeriesId,
        DateTime = m.DateTime,
        HomeTeamName = homeName ?? string.Empty,
        AwayTeamName = awayName ?? string.Empty,
        HomeIconPath = homeIcon,
        AwayIconPath = awayIcon,
        DisplayScore = BuildScoreText(m),
        ScoreBig = BuildScoreBig(m),
        Periods = BuildPeriods(m),
        IsLive = isLive,
        Status = m.Status
    };

    public static MatchRow Create(Match m, Team? home, Team? away, bool isLive) =>
        Create(m, home?.Name, away?.Name, home?.IconPath, away?.IconPath, isLive);

    /// <summary>Крупный счёт для карточки: «3 : 2» либо «— : —».</summary>
    private static string BuildScoreBig(Match m)
    {
        if (m.HomeGoals is null || m.AwayGoals is null)
            return "— : —";

        var h = m.HomeGoals.Value;
        var a = m.AwayGoals.Value;
        if (m.Status == MatchStatus.Finished && m.OutcomeType is not null)
        {
            var eff = MatchOutcomeResolver.GetEffectiveFinalScore(m);
            h = eff?.HomeGoals ?? h;
            a = eff?.AwayGoals ?? a;
        }
        return $"{h} : {a}";
    }

    /// <summary>Разбивка по периодам: «1:0 · 1:1 · 1:1 ОТ» (пусто, если нет).</summary>
    private static string BuildPeriods(Match m)
    {
        var parts = new List<string>();
        if (m.PeriodScores is { Count: > 0 } periods)
        {
            foreach (var p in periods)
            {
                var s = $"{p.HomeGoals}:{p.AwayGoals}";
                s += p.PeriodType switch
                {
                    PeriodType.Overtime => " ОТ",
                    PeriodType.Shootout => " Б",
                    _ => ""
                };
                parts.Add(s);
            }
        }

        // Если буллиты были, но отдельного периода нет — добавим счёт серии.
        var hasShootoutPeriod = m.PeriodScores?.Any(p => p.PeriodType == PeriodType.Shootout) == true;
        if (m.OutcomeType == OutcomeType.Shootout && !hasShootoutPeriod &&
            m.ShootoutScoreHome is { } sh && m.ShootoutScoreAway is { } sa)
        {
            parts.Add($"{sh}:{sa} Б");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>Полная строка счёта со скобками-периодами (старый формат, совместимость).</summary>
    private static string BuildScoreText(Match match)
    {
        if (match.HomeGoals is null || match.AwayGoals is null)
            return "— : —";

        var finalHomeGoals = match.HomeGoals.Value;
        var finalAwayGoals = match.AwayGoals.Value;
        if (match.Status == MatchStatus.Finished && match.OutcomeType is not null)
        {
            var effectiveScore = MatchOutcomeResolver.GetEffectiveFinalScore(match);
            finalHomeGoals = effectiveScore?.HomeGoals ?? finalHomeGoals;
            finalAwayGoals = effectiveScore?.AwayGoals ?? finalAwayGoals;
        }

        var baseScore = $"{finalHomeGoals}:{finalAwayGoals}";
        var periodPart = "";
        if (match.PeriodScores is { Count: > 0 } periods)
        {
            var parts = periods.Select(p =>
            {
                var s = $"{p.HomeGoals}:{p.AwayGoals}";
                return p.PeriodType == PeriodType.Overtime ? $"{s} ОТ" : p.PeriodType == PeriodType.Shootout ? $"{s} Б" : s;
            });
            periodPart = " (" + string.Join(", ", parts) + ")";
        }

        var hasOvertimePeriod = match.PeriodScores?.Any(p => p.PeriodType == PeriodType.Overtime) == true;
        var hasShootoutPeriod = match.PeriodScores?.Any(p => p.PeriodType == PeriodType.Shootout) == true;

        var outcomeSuffix = match.OutcomeType switch
        {
            OutcomeType.Overtime => hasOvertimePeriod ? "" : " ОТ",
            OutcomeType.Shootout => hasShootoutPeriod ? "" : match.ShootoutScoreHome is { } sh && match.ShootoutScoreAway is { } sa
                ? $" Б ({sh}:{sa})"
                : " Б",
            _ => ""
        };
        return $"{baseScore}{outcomeSuffix}{periodPart}";
    }
}
