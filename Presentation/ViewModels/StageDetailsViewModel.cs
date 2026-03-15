using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class StageDetailsViewModel : INotifyPropertyChanged
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IStageRepository _stageRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly StatsService _statsService;

    private Tournament? _tournament;
    private Stage? _stage;

    public Tournament? Tournament
    {
        get => _tournament;
        private set => SetField(ref _tournament, value);
    }

    public Stage? Stage
    {
        get => _stage;
        private set
        {
            if (SetField(ref _stage, value))
                OnPropertyChanged(nameof(PageTitle));
        }
    }

    public string PageTitle => Stage?.Name ?? string.Empty;

    public ObservableCollection<MatchRow> StageMatches { get; } = new();
    public ObservableCollection<StandingGroup> StandingsByGroupForStage { get; } = new();

    public StageDetailsViewModel(
        ITournamentRepository tournamentRepository,
        ITeamRepository teamRepository,
        IStageRepository stageRepository,
        IMatchRepository matchRepository,
        StatsService statsService)
    {
        _tournamentRepository = tournamentRepository;
        _teamRepository = teamRepository;
        _stageRepository = stageRepository;
        _matchRepository = matchRepository;
        _statsService = statsService;
    }

    public async Task LoadAsync(Guid tournamentId, Guid stageId)
    {
        Tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (Tournament is null)
            return;

        Stage = await _stageRepository.GetByIdAsync(stageId);
        if (Stage is null)
            return;

        var allMatches = await _matchRepository.GetByTournamentAsync(tournamentId);
        var stageMatches = allMatches
            .Where(m => m.StageId == stageId)
            .OrderBy(m => m.DateTime ?? DateTime.MinValue)
            .ToList();

        var teams = await _teamRepository.GetByTournamentAsync(tournamentId);
        var teamById = teams.ToDictionary(t => t.Id);

        var rows = new List<MatchRow>();
        foreach (var m in stageMatches)
        {
            teamById.TryGetValue(m.HomeTeamId, out var homeTeam);
            teamById.TryGetValue(m.AwayTeamId, out var awayTeam);
            rows.Add(CreateMatchRow(m, homeTeam?.Name, awayTeam?.Name, m.Status == MatchStatus.InProgress));
        }

        var stageStandingsGroups = BuildStandingsByGroupFromMatches(Tournament, teams, stageMatches);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            StageMatches.Clear();
            foreach (var r in rows)
                StageMatches.Add(r);

            StandingsByGroupForStage.Clear();
            foreach (var g in stageStandingsGroups)
                StandingsByGroupForStage.Add(g);
        });
    }

    public async Task SetMatchStatusAsync(Guid matchId, MatchStatus status)
    {
        if (Tournament is null)
            return;

        var matches = await _matchRepository.GetByTournamentAsync(Tournament.Id);
        var match = matches.FirstOrDefault(m => m.Id == matchId);
        if (match is null) return;

        match.Status = status;
        await _matchRepository.SaveAsync(match);

        if (Stage is not null)
            await LoadAsync(Tournament.Id, Stage.Id);
    }

    private List<StandingGroup> BuildStandingsByGroupFromMatches(Tournament tournament, IReadOnlyList<Team> teams, List<Match> matches)
    {
        var result = new List<StandingGroup>();
        var standings = _statsService.CalculateStandings(tournament, teams, matches);
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
        var groups = rules.Groups?.OrderBy(g => g.Order).ThenBy(g => g.Name).ToList() ?? new List<GroupInfo>();

        StandingRow CreateRow(Standing s, Team team, int place, string groupName, IReadOnlyList<int> last5)
        {
            return new StandingRow
            {
                Place = place,
                GroupName = groupName,
                TeamName = string.IsNullOrWhiteSpace(team.Name) ? "—" : team.Name,
                TeamIconPath = team.IconPath,
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
                var teamIdsInGroup = teams.Where(t => t.GroupId == group.Id).Select(t => t.Id).ToHashSet();
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
            var noGroupTeamIds = teams.Where(t => !t.GroupId.HasValue).Select(t => t.Id).ToHashSet();
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

    private static MatchRow CreateMatchRow(Match m, string? homeName, string? awayName, bool isLive) =>
        new()
        {
            MatchId = m.Id,
            DateTime = m.DateTime,
            HomeTeamName = homeName ?? string.Empty,
            AwayTeamName = awayName ?? string.Empty,
            DisplayScore = BuildScoreText(m),
            IsLive = isLive,
            Status = m.Status
        };

    private static IReadOnlyList<int> GetLast5ResultsForTeam(Guid teamId, List<Match> finishedOrderedByDateDesc)
    {
        var list = new List<int>(5);
        foreach (var m in finishedOrderedByDateDesc.Take(50))
        {
            if (m.HomeTeamId != teamId && m.AwayTeamId != teamId)
                continue;
            var isHome = m.HomeTeamId == teamId;
            var won = (isHome && m.HomeGoals! > m.AwayGoals!) || (!isHome && m.AwayGoals! > m.HomeGoals!);
            list.Add(won ? 1 : 2);
            if (list.Count >= 5)
                break;
        }
        while (list.Count < 5)
            list.Add(0);
        return list;
    }

    private static string FormatPointsPercentage(int points, int games, int maxPointsPerGame)
    {
        if (games == 0 || maxPointsPerGame <= 0)
            return "0";
        var pct = (double)points / (games * maxPointsPerGame) * 100.0;
        var rounded = Math.Round(pct, 2, MidpointRounding.AwayFromZero);
        return rounded.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
    }

    private static string BuildScoreText(Match match)
    {
        if (match.HomeGoals is null || match.AwayGoals is null || match.OutcomeType is null)
        {
            return "— : —";
        }

        var baseScore = $"{match.HomeGoals}:{match.AwayGoals}";
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

        var outcomeSuffix = match.OutcomeType switch
        {
            OutcomeType.Overtime => " ОТ",
            OutcomeType.Shootout => match.ShootoutScoreHome is { } sh && match.ShootoutScoreAway is { } sa
                ? $" Б ({sh}:{sa})"
                : " Б",
            _ => ""
        };
        return $"{baseScore}{outcomeSuffix}{periodPart}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

