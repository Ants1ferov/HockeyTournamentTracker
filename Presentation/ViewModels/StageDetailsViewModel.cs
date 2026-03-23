using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;
using System.Globalization;
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
    private readonly IStageColorZoneRepository _stageColorZoneRepository;
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
            {
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(IsPlayoffStage));
                OnPropertyChanged(nameof(IsSwissStage));
            }
        }
    }

    public string PageTitle => Stage?.Name ?? string.Empty;
    public bool IsPlayoffStage => Stage?.StageType == StageType.PlayOff;
    public bool IsSwissStage => Stage is not null && Stage.StageType != StageType.PlayOff;

    private bool _hasColorZonesForStandings;
    public bool HasColorZonesForStandings
    {
        get => _hasColorZonesForStandings;
        private set => SetField(ref _hasColorZonesForStandings, value);
    }

    public ObservableCollection<MatchRow> StageMatches { get; } = new();
    public ObservableCollection<StandingGroup> StandingsByGroupForStage { get; } = new();

    public ObservableCollection<TeamForAddUi> AvailableTeams { get; } = new();
    public ObservableCollection<StageGroupColumn> StageGroupColumns { get; } = new();
    public ObservableCollection<GroupInfo> StageGroups { get; } = new();
    public ObservableCollection<StageColorZoneListItem> ColorZones { get; } = new();
    public ObservableCollection<StageZonePickerRow> StageZonePickerRows { get; } = new();
    public ObservableCollection<TeamZoneAssignmentRow> TeamZoneAssignments { get; } = new();

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
        IStageColorZoneRepository stageColorZoneRepository,
        StatsService statsService)
    {
        _tournamentRepository = tournamentRepository;
        _teamRepository = teamRepository;
        _stageRepository = stageRepository;
        _matchRepository = matchRepository;
        _stageTeamRepository = stageTeamRepository;
        _stageGroupRepository = stageGroupRepository;
        _stageColorZoneRepository = stageColorZoneRepository;
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

        IReadOnlyList<StageColorZone>? swissZones = null;
        IReadOnlyDictionary<Guid, Guid?>? teamZoneIds = null;
        IReadOnlyDictionary<Guid, string>? zoneColorById = null;
        if (Stage.StageType == StageType.Swiss)
        {
            swissZones = await _stageColorZoneRepository.GetZonesByStageAsync(stageId);
            if (swissZones.Count > 0)
            {
                zoneColorById = swissZones.ToDictionary(z => z.Id, z => z.ColorHex);
                var assign = await _stageColorZoneRepository.GetTeamZoneAssignmentsAsync(stageId);
                teamZoneIds = assign.ToDictionary(kv => kv.Key, kv => (Guid?)kv.Value);
            }
        }

        var barW = Stage.StageType == StageType.Swiss && swissZones is { Count: > 0 } ? 6 : 0;
        var stageStandingsGroups = StandingsSectionBuilder.Build(
            _statsService,
            Tournament,
            teamsInStage,
            teamGroupIdsInStage,
            stageGroups,
            stageMatches,
            teamZoneIds,
            zoneColorById,
            barW);

        List<StageColorZoneListItem>? zoneItems = null;
        if (Stage.StageType == StageType.Swiss && swissZones is not null)
        {
            var usedZoneIds = stageStandingsGroups
                .SelectMany(g => g)
                .Where(r => r.ZoneId.HasValue)
                .Select(r => r.ZoneId!.Value)
                .ToHashSet();
            zoneItems = swissZones
                .OrderBy(z => z.SortOrder)
                .ThenBy(z => z.Name, StringComparer.Ordinal)
                .Select(z => new StageColorZoneListItem(
                    z.Id,
                    z.Name,
                    z.ColorHex,
                    !usedZoneIds.Contains(z.Id)))
                .ToList();
        }

        List<TeamZoneAssignmentRow>? assignmentRows = null;
        if (Stage.StageType == StageType.Swiss && swissZones is { Count: > 0 } && teamZoneIds is not null)
        {
            var zoneOrder = swissZones
                .OrderBy(z => z.SortOrder)
                .ThenBy(z => z.Name, StringComparer.Ordinal)
                .Select(z => z.Id)
                .ToList();
            assignmentRows = teamsInStage
                .OrderBy(t => t.Name, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true))
                .Select(t =>
                {
                    var idx = 0;
                    if (teamZoneIds.TryGetValue(t.Id, out var zNullable) && zNullable is { } zid)
                    {
                        for (var i = 0; i < zoneOrder.Count; i++)
                        {
                            if (zoneOrder[i] == zid)
                            {
                                idx = i + 1;
                                break;
                            }
                        }
                    }
                    return new TeamZoneAssignmentRow(t.Id, t.Name, t.IconPath, idx);
                })
                .ToList();
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            StageMatches.Clear();
            foreach (var r in rows)
                StageMatches.Add(r);

            StandingsByGroupForStage.Clear();
            foreach (var g in stageStandingsGroups)
                StandingsByGroupForStage.Add(g);

            ColorZones.Clear();
            if (zoneItems is not null)
                foreach (var z in zoneItems)
                    ColorZones.Add(z);

            StageZonePickerRows.Clear();
            if (Stage.StageType == StageType.Swiss && swissZones is not null)
            {
                StageZonePickerRows.Add(new StageZonePickerRow { ZoneId = null, Title = "Без зоны" });
                foreach (var z in swissZones.OrderBy(x => x.SortOrder).ThenBy(x => x.Name, StringComparer.Ordinal))
                    StageZonePickerRows.Add(new StageZonePickerRow { ZoneId = z.Id, Title = z.Name });
            }

            TeamZoneAssignments.Clear();
            if (assignmentRows is not null)
                foreach (var r in assignmentRows)
                    TeamZoneAssignments.Add(r);

            HasColorZonesForStandings = Stage.StageType == StageType.Swiss && swissZones is { Count: > 0 };

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

    public async Task AddColorZoneAsync()
    {
        if (Tournament is null || Stage is null || Stage.StageType != StageType.Swiss)
            return;

        var existing = await _stageColorZoneRepository.GetZonesByStageAsync(Stage.Id);
        var zone = new StageColorZone
        {
            Id = Guid.NewGuid(),
            StageId = Stage.Id,
            Name = $"Зона {existing.Count + 1}",
            ColorHex = "#3B82F6",
            SortOrder = existing.Count
        };
        await _stageColorZoneRepository.SaveZoneAsync(zone);
        await LoadAsync(Tournament.Id, Stage.Id);
    }

    public async Task DeleteColorZoneAsync(Guid zoneId)
    {
        if (Tournament is null || Stage is null)
            return;
        await _stageColorZoneRepository.DeleteZoneAsync(zoneId);
        await LoadAsync(Tournament.Id, Stage.Id);
    }

    public async Task UpdateZoneColorAsync(Guid zoneId, string colorHex)
    {
        if (Tournament is null || Stage is null)
            return;
        var z = await _stageColorZoneRepository.GetZoneByIdAsync(zoneId);
        if (z is null)
            return;
        z.ColorHex = colorHex;
        await _stageColorZoneRepository.SaveZoneAsync(z);
        await LoadAsync(Tournament.Id, Stage.Id);
    }

    public async Task RenameZoneAsync(Guid zoneId, string newName)
    {
        if (Tournament is null || Stage is null || string.IsNullOrWhiteSpace(newName))
            return;
        var z = await _stageColorZoneRepository.GetZoneByIdAsync(zoneId);
        if (z is null)
            return;
        z.Name = newName.Trim();
        await _stageColorZoneRepository.SaveZoneAsync(z);
        await LoadAsync(Tournament.Id, Stage.Id);
    }

    public async Task SetStandingRowZoneFromPickerAsync(Guid teamId, int pickerIndex)
    {
        if (Tournament is null || Stage is null || Stage.StageType != StageType.Swiss)
            return;
        Guid? zoneId = null;
        if (pickerIndex > 0 && pickerIndex < StageZonePickerRows.Count)
            zoneId = StageZonePickerRows[pickerIndex].ZoneId;
        await SetStandingRowZoneAsync(teamId, zoneId);
    }

    public async Task SetStandingRowZoneAsync(Guid teamId, Guid? zoneId)
    {
        if (Tournament is null || Stage is null || Stage.StageType != StageType.Swiss)
            return;
        await _stageColorZoneRepository.SetTeamZoneAsync(Stage.Id, teamId, zoneId);
        await LoadAsync(Tournament.Id, Stage.Id);
    }

    private static MatchRow CreateMatchRow(Match m, string? homeName, string? awayName, bool isLive) =>
        new()
        {
            MatchId = m.Id,
            SeriesId = m.SeriesId,
            DateTime = m.DateTime,
            HomeTeamName = homeName ?? string.Empty,
            AwayTeamName = awayName ?? string.Empty,
            DisplayScore = BuildScoreText(m),
            IsLive = isLive,
            Status = m.Status
        };

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

public sealed class TeamZoneAssignmentRow
{
    public Guid TeamId { get; }
    public string TeamName { get; }
    public string? IconPath { get; }
    public int ZonePickerIndex { get; set; }

    public TeamZoneAssignmentRow(Guid teamId, string teamName, string? iconPath, int zonePickerIndex)
    {
        TeamId = teamId;
        TeamName = teamName;
        IconPath = iconPath;
        ZonePickerIndex = zonePickerIndex;
    }
}

public sealed class StageColorZoneListItem
{
    public Guid ZoneId { get; }
    public string Name { get; }
    public string ColorHex { get; }
    /// <summary>True — зона не назначена ни одной строке таблицы.</summary>
    public bool ShowUnusedWarning { get; }

    public StageColorZoneListItem(Guid zoneId, string name, string colorHex, bool showUnusedWarning)
    {
        ZoneId = zoneId;
        Name = name;
        ColorHex = colorHex;
        ShowUnusedWarning = showUnusedWarning;
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

