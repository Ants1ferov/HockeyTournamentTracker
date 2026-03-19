namespace HockeyTournamentTracker.Domain;

public static class MatchOutcomeResolver
{
    public static bool TryGetWinnerTeamId(Match match, out Guid winnerTeamId)
    {
        winnerTeamId = Guid.Empty;

        if (match.OutcomeType == OutcomeType.Shootout)
        {
            if (match.ShootoutScoreHome is { } sh && match.ShootoutScoreAway is { } sa && sh != sa)
            {
                winnerTeamId = sh > sa ? match.HomeTeamId : match.AwayTeamId;
                return true;
            }

            if (match.WinnerTeamId is { } storedWinner &&
                (storedWinner == match.HomeTeamId || storedWinner == match.AwayTeamId))
            {
                winnerTeamId = storedWinner;
                return true;
            }
        }

        if (match.HomeGoals is not { } hg || match.AwayGoals is not { } ag || hg == ag)
            return false;

        winnerTeamId = hg > ag ? match.HomeTeamId : match.AwayTeamId;
        return true;
    }

    public static bool TryGetLoserTeamId(Match match, out Guid loserTeamId)
    {
        loserTeamId = Guid.Empty;
        if (!TryGetWinnerTeamId(match, out var winnerTeamId))
            return false;

        loserTeamId = winnerTeamId == match.HomeTeamId ? match.AwayTeamId : match.HomeTeamId;
        return true;
    }

    public static (int HomeGoals, int AwayGoals)? GetEffectiveFinalScore(Match match)
    {
        if (match.HomeGoals is not { } homeGoals || match.AwayGoals is not { } awayGoals)
            return null;

        if (match.OutcomeType == OutcomeType.Shootout &&
            homeGoals == awayGoals &&
            TryGetWinnerTeamId(match, out var winnerTeamId))
        {
            if (winnerTeamId == match.HomeTeamId)
                homeGoals += 1;
            else if (winnerTeamId == match.AwayTeamId)
                awayGoals += 1;
        }

        return (homeGoals, awayGoals);
    }
}
