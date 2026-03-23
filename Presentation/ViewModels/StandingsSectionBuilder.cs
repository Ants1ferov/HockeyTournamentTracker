using System.Collections.ObjectModel;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

internal static class StandingsSectionBuilder
{
    public static List<StandingGroup> Build(
        StatsService statsService,
        Tournament tournament,
        IReadOnlyList<Team> teams,
        IReadOnlyDictionary<Guid, Guid?> teamGroupIdsInStage,
        IReadOnlyList<GroupInfo> stageGroups,
        List<Match> matches,
        IReadOnlyDictionary<Guid, Guid?>? teamZoneIds,
        IReadOnlyDictionary<Guid, string>? zoneColorByZoneId,
        IReadOnlyList<Guid>? pickerZoneOrder,
        int zoneBarColumnWidth = 0)
    {
        var result = new List<StandingGroup>();
        var standings = statsService.CalculateStandings(tournament, teams, matches);
        var teamById = teams.ToDictionary(t => t.Id);
        var finishedMatches = matches
            .Where(m => m.Status == MatchStatus.Finished && m.OutcomeType.HasValue && m.HomeGoals.HasValue && m.AwayGoals.HasValue)
            .OrderByDescending(m => m.DateTime ?? DateTime.MinValue)
            .ToList();
        var maxPointsPerGame = tournament.Rules != null
            ? Math.Max(tournament.Rules.PointsForRegulationWin,
                Math.Max(tournament.Rules.PointsForOvertimeWin, tournament.Rules.PointsForShootoutWin))
            : 3;
        var rules = tournament.Rules ?? new TournamentRules();
        var groups = stageGroups.ToList();

        StandingRow CreateRow(Standing s, Team team, int place, string groupName, IReadOnlyList<int> last5)
        {
            Guid? zid = null;
            string? zoneHex = null;
            var pickerIndex = 0;
            if (teamZoneIds is not null && teamZoneIds.TryGetValue(team.Id, out var z) && z.HasValue)
            {
                zid = z;
                if (zoneColorByZoneId is not null && zoneColorByZoneId.TryGetValue(z.Value, out var hx))
                    zoneHex = hx;
                if (pickerZoneOrder is not null)
                {
                    var idx = pickerZoneOrder.IndexOf(z.Value);
                    pickerIndex = idx >= 0 ? idx + 1 : 0;
                }
            }

            return new StandingRow
            {
                TeamId = team.Id,
                ZoneId = zid,
                ZonePickerIndex = pickerIndex,
                ZoneBarColumnWidth = zoneBarColumnWidth,
                Place = place,
                GroupName = groupName,
                TeamName = string.IsNullOrWhiteSpace(team.Name) ? "—" : team.Name,
                TeamIconPath = team.IconPath,
                ZoneBarColorHex = zoneHex,
                Games = s.Games,
                WinsReg = s.WinsRegulation,
                WinsOt = s.WinsOvertime,
                WinsSo = s.WinsShootout,
                LossesReg = s.LossesRegulation,
                LossesOt = s.LossesOvertime,
                LossesSo = s.LossesShootout,
                GoalsFor = s.GoalsFor,
                GoalsAgainst = s.GoalsAgainst,
                GoalDiff = s.GoalDifference,
                Last5Results = last5,
                PointsPct = FormatPointsPercentage(s.Points, s.Games, maxPointsPerGame),
                Points = s.Points
            };
        }

        if (groups.Count > 0)
        {
            foreach (var group in groups)
            {
                var teamIdsInGroup = teams
                    .Where(t => teamGroupIdsInStage.TryGetValue(t.Id, out var gid) && gid == group.Id)
                    .Select(t => t.Id)
                    .ToHashSet();
                var inGroup = standings.Where(s => teamIdsInGroup.Contains(s.TeamId)).ToList();
                var sortedInGroup = StatsService.SortByRules(inGroup, rules);
                var groupRows = new ObservableCollection<StandingRow>();
                var place = 1;
                foreach (var s in sortedInGroup)
                {
                    if (!teamById.TryGetValue(s.TeamId, out var team))
                        continue;
                    var last5 = GetLast5ResultsForTeam(s.TeamId, finishedMatches);
                    groupRows.Add(CreateRow(s, team, place++, group.Name, last5));
                }
                if (groupRows.Count > 0)
                {
                    var g = new StandingGroup { GroupName = group.Name };
                    foreach (var row in groupRows) g.Add(row);
                    result.Add(g);
                }
            }
            var noGroupTeamIds = teams
                .Where(t => !teamGroupIdsInStage.TryGetValue(t.Id, out var gid) || gid is null)
                .Select(t => t.Id)
                .ToHashSet();
            var noGroup = standings.Where(s => noGroupTeamIds.Contains(s.TeamId)).ToList();
            if (noGroup.Count > 0)
            {
                var sortedNoGroup = StatsService.SortByRules(noGroup, rules);
                var noGroupRows = new ObservableCollection<StandingRow>();
                var place = 1;
                foreach (var s in sortedNoGroup)
                {
                    if (!teamById.TryGetValue(s.TeamId, out var team))
                        continue;
                    var last5 = GetLast5ResultsForTeam(s.TeamId, finishedMatches);
                    noGroupRows.Add(CreateRow(s, team, place++, "—", last5));
                }
                var noGr = new StandingGroup { GroupName = "—" };
                foreach (var row in noGroupRows) noGr.Add(row);
                result.Add(noGr);
            }
        }
        else
        {
            var place = 1;
            var allRows = new ObservableCollection<StandingRow>();
            foreach (var s in standings)
            {
                if (!teamById.TryGetValue(s.TeamId, out var team))
                    continue;
                var last5 = GetLast5ResultsForTeam(s.TeamId, finishedMatches);
                allRows.Add(CreateRow(s, team, place++, string.Empty, last5));
            }
            if (allRows.Count > 0)
            {
                var allGr = new StandingGroup { GroupName = string.Empty };
                foreach (var row in allRows) allGr.Add(row);
                result.Add(allGr);
            }
        }

        return result;
    }

    private static string FormatPointsPercentage(int points, int games, int maxPointsPerGame)
    {
        if (games == 0 || maxPointsPerGame <= 0)
            return "0";
        var pct = (double)points / (games * maxPointsPerGame) * 100.0;
        var rounded = Math.Round(pct, 2, MidpointRounding.AwayFromZero);
        return rounded.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
    }

    private static IReadOnlyList<int> GetLast5ResultsForTeam(Guid teamId, List<Match> finishedOrderedByDateDesc)
    {
        var list = new List<int>(5);
        foreach (var m in finishedOrderedByDateDesc)
        {
            if (m.HomeTeamId != teamId && m.AwayTeamId != teamId)
                continue;
            if (!MatchOutcomeResolver.TryGetWinnerTeamId(m, out var winnerTeamId))
                continue;

            list.Add(winnerTeamId == teamId ? 1 : 2);
            if (list.Count >= 5)
                break;
        }
        while (list.Count < 5)
            list.Add(0);
        return list;
    }
}
