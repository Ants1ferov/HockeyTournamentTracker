using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;
using System.Linq;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class StageDetailsViewModel : INotifyPropertyChanged
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IStageRepository _stageRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IStageTeamRepository _stageTeamRepository;
    private readonly IStageGroupRepository _stageGroupRepository;
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

    public ObservableCollection<TeamForAddUi> AvailableTeams { get; } = new();
    public ObservableCollection<StageGroupColumn> StageGroupColumns { get; } = new();
    public ObservableCollection<GroupInfo> StageGroups { get; } = new();

    private readonly HashSet<Guid> _moveSelectedTeamIds = new();
    public int MoveSelectedCount => _moveSelectedTeamIds.Count;

    public ICommand MoveSelectedTeamsToGroupCommand { get; }

    public StageDetailsViewModel(
        ITournamentRepository tournamentRepository,
        ITeamRepository teamRepository,
        IStageRepository stageRepository,
        IMatchRepository matchRepository,
        IStageTeamRepository stageTeamRepository,
        IStageGroupRepository stageGroupRepository,
        StatsService statsService)
    {
        _tournamentRepository = tournamentRepository;
        _teamRepository = teamRepository;
        _stageRepository = stageRepository;
        _matchRepository = matchRepository;
        _stageTeamRepository = stageTeamRepository;
        _stageGroupRepository = stageGroupRepository;
        _statsService = statsService;

        MoveSelectedTeamsToGroupCommand = new Command<Guid?>(async gid =>
        {
            await MoveSelectedTeamsToGroupAsync(gid);
        });
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
            .OrderByDescending(m => m.DateTime ?? DateTime.MinValue)
            .ToList();

        var allTeams = await _teamRepository.GetByTournamentAsync(tournamentId);
        var teamById = allTeams.ToDictionary(t => t.Id);

        var stageTeamIds = (await _stageTeamRepository.GetTeamIdsByStageAsync(stageId)).ToHashSet();
        if (stageTeamIds.Count == 0 && stageMatches.Count > 0)
        {
            // Backward-compatibility: если StageTeams пустая, но матчи уже есть — используем команды из матчей.
            var fromMatches = stageMatches
                .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
                .Distinct()
                .ToList();
            stageTeamIds = fromMatches.ToHashSet();

            // Пробуем инициализировать GroupId на уровне StageTeams из глобального Team.GroupId,
            // чтобы старые турниры визуально не "провалились" в Без группы.
            var teamGroupByTeamId = fromMatches.ToDictionary(
                id => id,
                id => teamById.TryGetValue(id, out var t) ? t.GroupId : null);

            await _stageTeamRepository.AddTeamsToStageAsync(stageId, teamGroupByTeamId);
        }

        var teamsInStage = allTeams.Where(t => stageTeamIds.Contains(t.Id)).ToList();
        var teamGroupIdsInStage = await _stageTeamRepository.GetTeamGroupIdsByStageAsync(stageId);

        // При любом повторном открытии/перезагрузке страницы сбрасываем выбор команд для перемещения.
        _moveSelectedTeamIds.Clear();

        // Группы управляются внутри конкретной стадии.
        var stageGroups = (await _stageGroupRepository.GetByStageAsync(stageId)).ToList();
        if (stageGroups.Count == 0 && Tournament.Rules?.Groups is { Count: > 0 } globalGroups)
        {
            foreach (var g in globalGroups.OrderBy(x => x.Order).ThenBy(x => x.Name))
                await _stageGroupRepository.AddGroupAsync(stageId, g.Name);

            stageGroups = (await _stageGroupRepository.GetByStageAsync(stageId)).ToList();
        }

        var stageGroupColumns = BuildStageGroupColumns(stageGroups, teamsInStage, teamGroupIdsInStage);
        var availableTeams = allTeams.Where(t => !stageTeamIds.Contains(t.Id)).ToList();

        var rows = new List<MatchRow>();
        foreach (var m in stageMatches)
        {
            teamById.TryGetValue(m.HomeTeamId, out var homeTeam);
            teamById.TryGetValue(m.AwayTeamId, out var awayTeam);
            rows.Add(CreateMatchRow(m, homeTeam?.Name, awayTeam?.Name, m.Status == MatchStatus.InProgress));
        }

        var stageStandingsGroups = BuildStandingsByGroupFromMatches(
            Tournament,
            teamsInStage,
            teamGroupIdsInStage,
            stageGroups,
            stageMatches);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            StageMatches.Clear();
            foreach (var r in rows)
                StageMatches.Add(r);

            StandingsByGroupForStage.Clear();
            foreach (var g in stageStandingsGroups)
                StandingsByGroupForStage.Add(g);

            StageGroups.Clear();
            foreach (var g in stageGroups)
                StageGroups.Add(g);

            StageGroupColumns.Clear();
            foreach (var c in stageGroupColumns)
                StageGroupColumns.Add(c);

            AvailableTeams.Clear();
            foreach (var t in availableTeams)
                AvailableTeams.Add(new TeamForAddUi(t, false));
        });
    }

    private List<StageGroupColumn> BuildStageGroupColumns(
        IReadOnlyList<GroupInfo> stageGroups,
        IReadOnlyList<Team> teamsInStage,
        IReadOnlyDictionary<Guid, Guid?> teamGroupIdsInStage)
    {
        var result = new List<StageGroupColumn>();

        foreach (var group in stageGroups)
        {
            var groupTeams = teamsInStage
                .Where(t => teamGroupIdsInStage.TryGetValue(t.Id, out var gid) && gid == group.Id)
                .OrderBy(t => t.Name)
                .Select(t => new StageTeamForUi(t, _moveSelectedTeamIds.Contains(t.Id)))
                .ToList();

            result.Add(new StageGroupColumn(group.Id, group.Name, groupTeams));
        }

        var noGroupTeams = teamsInStage
            .Where(t => !teamGroupIdsInStage.TryGetValue(t.Id, out var gid) || gid is null)
            .OrderBy(t => t.Name)
            .Select(t => new StageTeamForUi(t, _moveSelectedTeamIds.Contains(t.Id)))
            .ToList();

        // Колонку без группы показываем только если есть команды.
        if (noGroupTeams.Count > 0)
            result.Add(new StageGroupColumn(null, "Без группы", noGroupTeams));

        return result;
    }

    public async Task AddAllTeamsToStageAsync()
    {
        if (Stage is null || Tournament is null)
            return;

        var ids = AvailableTeams.Select(t => t.TeamId).ToList();
        await _stageTeamRepository.AddTeamsToStageAsync(Stage.Id, ids);
        await LoadAsync(Tournament.Id, Stage.Id);
    }

    public async Task AddSelectedTeamsToStageAsync(IReadOnlyList<Guid> teamIds)
    {
        if (Stage is null || Tournament is null)
            return;

        await _stageTeamRepository.AddTeamsToStageAsync(Stage.Id, teamIds);
        await LoadAsync(Tournament.Id, Stage.Id);
    }

    public async Task AddGroupAsync(string name)
    {
        if (Stage is null || Tournament is null) return;
        if (string.IsNullOrWhiteSpace(name)) return;

        await _stageGroupRepository.AddGroupAsync(Stage.Id, name.Trim());
        await LoadAsync(Tournament.Id, Stage.Id);
    }

    public async Task RenameGroupAsync(Guid groupId, string newName)
    {
        if (Stage is null || Tournament is null) return;
        if (groupId == Guid.Empty) return;
        if (string.IsNullOrWhiteSpace(newName)) return;

        await _stageGroupRepository.RenameGroupAsync(Stage.Id, groupId, newName.Trim());
        await LoadAsync(Tournament.Id, Stage.Id);
    }

    public async Task DeleteGroupAsync(Guid groupId)
    {
        if (Stage is null || Tournament is null) return;
        if (groupId == Guid.Empty) return;

        await _stageGroupRepository.DeleteGroupAsync(Stage.Id, groupId);
        await LoadAsync(Tournament.Id, Stage.Id);
    }

    public void ToggleMoveTeamSelection(Guid teamId)
    {
        if (_moveSelectedTeamIds.Contains(teamId))
            _moveSelectedTeamIds.Remove(teamId);
        else
            _moveSelectedTeamIds.Add(teamId);

        // Обновляем флажки выбора в UI-объектах
        foreach (var col in StageGroupColumns)
        foreach (var chip in col.Teams)
            if (chip.TeamId == teamId)
                chip.IsSelectedForMove = _moveSelectedTeamIds.Contains(teamId);

        OnPropertyChanged(nameof(MoveSelectedCount));
    }

    public async Task MoveSelectedTeamsToGroupAsync(Guid? targetGroupId)
    {
        if (Stage is null || Tournament is null)
            return;
        if (_moveSelectedTeamIds.Count == 0)
            return;

        await _stageTeamRepository.SetTeamsGroupIdAsync(
            Stage.Id,
            _moveSelectedTeamIds.ToList(),
            targetGroupId);

        _moveSelectedTeamIds.Clear();
        await LoadAsync(Tournament.Id, Stage!.Id);
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

    public async Task DeleteMatchAsync(Guid matchId)
    {
        if (Tournament is null || Stage is null)
            return;

        await _matchRepository.DeleteAsync(matchId);
        await LoadAsync(Tournament.Id, Stage.Id);
    }

    private List<StandingGroup> BuildStandingsByGroupFromMatches(
        Tournament tournament,
        IReadOnlyList<Team> teams,
        IReadOnlyDictionary<Guid, Guid?> teamGroupIdsInStage,
        IReadOnlyList<GroupInfo> stageGroups,
        List<Match> matches)
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
        var groups = stageGroups.ToList();

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

        var effectiveScore = MatchOutcomeResolver.GetEffectiveFinalScore(match);
        var finalHomeGoals = effectiveScore?.HomeGoals ?? match.HomeGoals.Value;
        var finalAwayGoals = effectiveScore?.AwayGoals ?? match.AwayGoals.Value;
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
            OutcomeType.Shootout => hasShootoutPeriod ? "" : (match.ShootoutScoreHome is { } sh && match.ShootoutScoreAway is { } sa
                ? $" Б ({sh}:{sa})"
                : " Б"),
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

public sealed class StageGroupColumn
{
    public Guid? GroupId { get; }
    public string Title { get; }

    public ObservableCollection<StageTeamForUi> Teams { get; } = new();

    public StageGroupColumn(Guid? groupId, string title, IReadOnlyList<StageTeamForUi> teams)
    {
        GroupId = groupId;
        Title = title;
        foreach (var t in teams)
            Teams.Add(t);
    }
}

public sealed class StageTeamForUi : INotifyPropertyChanged
{
    public Team Team { get; }
    public Guid TeamId => Team.Id;
    public string Name => Team.Name;
    public string? IconPath => Team.IconPath;

    private bool _isSelectedForMove;
    public bool IsSelectedForMove
    {
        get => _isSelectedForMove;
        set
        {
            if (_isSelectedForMove == value) return;
            _isSelectedForMove = value;
            OnPropertyChanged(nameof(IsSelectedForMove));
        }
    }

    public StageTeamForUi(Team team, bool isSelectedForMove)
    {
        Team = team;
        _isSelectedForMove = isSelectedForMove;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class TeamForAddUi : INotifyPropertyChanged
{
    public Team Team { get; }
    public Guid TeamId => Team.Id;
    public string Name => Team.Name;
    public string? IconPath => Team.IconPath;

    private bool _isSelectedForAdd;
    public bool IsSelectedForAdd
    {
        get => _isSelectedForAdd;
        set
        {
            if (_isSelectedForAdd == value) return;
            _isSelectedForAdd = value;
            OnPropertyChanged(nameof(IsSelectedForAdd));
        }
    }

    public TeamForAddUi(Team team, bool isSelectedForAdd)
    {
        Team = team;
        _isSelectedForAdd = isSelectedForAdd;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

