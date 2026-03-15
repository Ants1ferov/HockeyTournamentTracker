namespace HockeyTournamentTracker.Data;

public static class TeamIconHelper
{
    private static readonly string TeamIconsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TeamIcons");

    public static string GetIconPath(Guid tournamentId, Guid teamId)
    {
        Directory.CreateDirectory(TeamIconsDir);
        return Path.Combine(TeamIconsDir, $"{tournamentId:N}_{teamId:N}.png");
    }
}
