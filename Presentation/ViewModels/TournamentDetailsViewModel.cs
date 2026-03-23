using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class TournamentDetailsViewModel : INotifyPropertyChanged
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IStageRepository _stageRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IStageTeamRepository _stageTeamRepository;
    private readonly IStageGroupRepository _stageGroupRepository;
    private readonly IStageColorZoneRepository _stageColorZoneRepository;
    private readonly StatsService _statsService;
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    private Tournament? _tournament;
    private int _selectedTabIndex;
    private Stage? _selectedStage;
    private string _pointsRegWinText = "—";
    private string _pointsOtWinText = "—";
    private string _pointsSoWinText = "—";
    private string _pointsRegLossText = "—";
    private string _pointsOtLossText = "—";
    private string _pointsSoLossText = "—";
    private int _stageSelectionVersion;

    public Tournament? Tournament
    {
        get => _tournament;
        private set
        {
            if (SetField(ref _tournament, value))
            {
                OnPropertyChanged(nameof(StatusIndex));
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(TournamentDescription));
                UpdateRulesDisplay();
                UpdateSortOrderDisplay();
            }
        }
    }

    /// <summary>Заголовок страницы (безопасно при null Tournament).</summary>
    public string PageTitle => Tournament?.Name ?? string.Empty;

    /// <summary>Описание турнира для привязки (безопасно при null Tournament).</summary>
    public string TournamentDescription => Tournament?.Description ?? string.Empty;

    public string PointsRegWinText { get => _pointsRegWinText; private set => SetField(ref _pointsRegWinText, value); }
    public string PointsOtWinText { get => _pointsOtWinText; private set => SetField(ref _pointsOtWinText, value); }
    public string PointsSoWinText { get => _pointsSoWinText; private set => SetField(ref _pointsSoWinText, value); }
    public string PointsRegLossText { get => _pointsRegLossText; private set => SetField(ref _pointsRegLossText, value); }
    public string PointsOtLossText { get => _pointsOtLossText; private set => SetField(ref _pointsOtLossText, value); }
    public string PointsSoLossText { get => _pointsSoLossText; private set => SetField(ref _pointsSoLossText, value); }
    public int StatusIndex
    {
        get => (int)(Tournament?.Status ?? TournamentStatus.Planned);
        set
        {
            if (Tournament is null || value < 0 || value > 3) return;
            Tournament.Status = (TournamentStatus)value;
            OnPropertyChanged(nameof(StatusIndex));
            _ = _tournamentRepository.SaveAsync(Tournament);
        }
    }

    public ObservableCollection<StandingRow> Standings { get; } = new();
    public ObservableCollection<StandingGroup> StandingsByGroup { get; } = new();
    public ObservableCollection<MatchRow> LiveMatches { get; } = new();
    public ObservableCollection<MatchRow> Matches { get; } = new();
    public ObservableCollection<MatchRow> LastPlayedMatches { get; } = new();
    public ObservableCollection<MatchRow> UpcomingMatches { get; } = new();
    public ObservableCollection<Stage> Stages { get; } = new();
    public ObservableCollection<MatchRow> StageMatches { get; } = new();
    public ObservableCollection<StandingGroup> StandingsByGroupForStage { get; } = new();
    public ObservableCollection<Team> ParticipantTeams { get; } = new();
    public ObservableCollection<StageZoneLegendItem> StageZoneLegend { get; } = new();

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetField(ref _selectedTabIndex, value))
            {
                OnPropertyChanged(nameof(IsHomeTabSelected));
                OnPropertyChanged(nameof(IsStagesTabSelected));
                OnPropertyChanged(nameof(IsParticipantsTabSelected));
                OnPropertyChanged(nameof(IsStatisticsTabSelected));
                OnPropertyChanged(nameof(StatisticsTabLabelColorHex));
                OnPropertyChanged(nameof(HomeTabIcon));
                OnPropertyChanged(nameof(StagesTabIcon));
                OnPropertyChanged(nameof(ParticipantsTabIcon));
            }
        }
    }

    public bool IsHomeTabSelected => SelectedTabIndex == 0;
    public bool IsStagesTabSelected => SelectedTabIndex == 1;
    public bool IsParticipantsTabSelected => SelectedTabIndex == 2;
    public bool IsStatisticsTabSelected => SelectedTabIndex == 3;

    public string StatisticsTabLabelColorHex => IsStatisticsTabSelected ? "#DC2626" : "#374151";

    public string HomeTabIcon => IsHomeTabSelected ? "tourhomeclicked" : "tourhomenonclicked";
    public string StagesTabIcon => IsStagesTabSelected ? "tourstageclicked" : "tourstagenonclicked";
    public string ParticipantsTabIcon => IsParticipantsTabSelected ? "tourplayerclicked" : "tourplayernonclicked";
    public bool IsSelectedStageSwiss => SelectedStage?.StageType == StageType.Swiss;

    private bool _showStageZoneLegend;
    public bool ShowStageZoneLegend
    {
        get => _showStageZoneLegend;
        private set => SetField(ref _showStageZoneLegend, value);
    }

    public Stage? SelectedStage
    {
        get => _selectedStage;
        set
        {
            if (SetField(ref _selectedStage, value))
            {
                OnPropertyChanged(nameof(IsSelectedStageSwiss));
                RefreshStageMatches();
            }
        }
    }

    /// <summary>Порядок распределения мест для отображения на экране деталей турнира.</summary>
    public IReadOnlyList<StandingSortCriterion> SortOrderDisplay { get; private set; } = Array.Empty<StandingSortCriterion>();

    public TournamentDetailsViewModel(
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
        // Уведомляем начальное состояние вкладок, чтобы контент отобразился при первом показе страницы
        OnPropertyChanged(nameof(IsHomeTabSelected));
        OnPropertyChanged(nameof(IsStagesTabSelected));
        OnPropertyChanged(nameof(IsParticipantsTabSelected));
        OnPropertyChanged(nameof(IsStatisticsTabSelected));
        OnPropertyChanged(nameof(StatisticsTabLabelColorHex));
        OnPropertyChanged(nameof(HomeTabIcon));
        OnPropertyChanged(nameof(StagesTabIcon));
        OnPropertyChanged(nameof(ParticipantsTabIcon));
    }

    public async Task LoadAsync(Guid tournamentId)
    {
        await _loadSemaphore.WaitAsync();
        try
        {
            Tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
            if (Tournament is null)
                return;

            var teams = await _teamRepository.GetByTournamentAsync(tournamentId);
            var matches = await _matchRepository.GetByTournamentAsync(tournamentId);

            Standings.Clear();
            StandingsByGroup.Clear();
            var standings = _statsService.CalculateStandings(Tournament, teams, matches);
            var teamById = teams.ToDictionary(t => t.Id);

            var finishedMatches = matches
                .Where(m => m.Status == MatchStatus.Finished && m.OutcomeType.HasValue && m.HomeGoals.HasValue && m.AwayGoals.HasValue)
                .OrderByDescending(m => m.DateTime ?? DateTime.MinValue)
                .ToList();

            var maxPointsPerGame = Tournament.Rules != null
                ? Math.Max(Tournament.Rules.PointsForRegulationWin,
                    Math.Max(Tournament.Rules.PointsForOvertimeWin, Tournament.Rules.PointsForShootoutWin))
                : 3;

            var standingsList = standings;
            var rules = Tournament.Rules ?? new TournamentRules();
            var groups = rules.Groups?.OrderBy(g => g.Order).ThenBy(g => g.Name).ToList() ?? new List<GroupInfo>();

            StandingRow CreateStandingRow(Standing s, Team team, int place, string groupName, IReadOnlyList<int> last5)
            {
                return new StandingRow
                {
                    TeamId = team.Id,
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
                    var inGroup = standingsList.Where(s => teamIdsInGroup.Contains(s.TeamId)).ToList();
                    var sortedInGroup = StatsService.SortByRules(inGroup, rules);
                    var groupRows = new ObservableCollection<StandingRow>();
                    var place = 1;
                    foreach (var s in sortedInGroup)
                    {
                        if (!teamById.TryGetValue(s.TeamId, out var team))
                            continue;
                        var last5 = GetLast5ResultsForTeam(s.TeamId, finishedMatches);
                        groupRows.Add(CreateStandingRow(s, team, place++, group.Name, last5));
                    }
                    if (groupRows.Count > 0)
                    {
                        var g = new StandingGroup { GroupName = group.Name };
                        foreach (var row in groupRows) g.Add(row);
                        StandingsByGroup.Add(g);
                    }
                }
                var noGroupTeamIds = teams.Where(t => !t.GroupId.HasValue).Select(t => t.Id).ToHashSet();
                var noGroup = standingsList.Where(s => noGroupTeamIds.Contains(s.TeamId)).ToList();
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
                        noGroupRows.Add(CreateStandingRow(s, team, place++, "—", last5));
                    }
                    var noGr = new StandingGroup { GroupName = "—" };
                    foreach (var row in noGroupRows) noGr.Add(row);
                    StandingsByGroup.Add(noGr);
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
                    var pointsPct = FormatPointsPercentage(s.Points, s.Games, maxPointsPerGame);
                    allRows.Add(new StandingRow
                    {
                        TeamId = team.Id,
                        Place = place++,
                        GroupName = string.Empty,
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
                        PointsPct = pointsPct,
                        Points = s.Points
                    });
                }
                if (allRows.Count > 0)
                {
                    var allGr = new StandingGroup { GroupName = string.Empty };
                    foreach (var row in allRows) allGr.Add(row);
                    StandingsByGroup.Add(allGr);
                }
            }

            UpdateSortOrderDisplay();

            LiveMatches.Clear();
            Matches.Clear();

            var ordered = matches.OrderBy(m => m.DateTime ?? DateTime.MinValue).ToList();
            foreach (var m in ordered.Where(m => m.Status == MatchStatus.InProgress))
            {
                teamById.TryGetValue(m.HomeTeamId, out var homeTeam);
                teamById.TryGetValue(m.AwayTeamId, out var awayTeam);
                LiveMatches.Add(CreateMatchRow(m, homeTeam?.Name, awayTeam?.Name, isLive: true));
            }

            foreach (var m in ordered.Where(m => m.Status != MatchStatus.InProgress))
            {
                teamById.TryGetValue(m.HomeTeamId, out var homeTeam);
                teamById.TryGetValue(m.AwayTeamId, out var awayTeam);
                Matches.Add(CreateMatchRow(m, homeTeam?.Name, awayTeam?.Name, isLive: false));
            }

            LastPlayedMatches.Clear();
            UpcomingMatches.Clear();
            var lastPlayed = matches
                .Where(m => m.Status == MatchStatus.Finished)
                .OrderByDescending(m => m.DateTime ?? DateTime.MinValue)
                .Take(5)
                .ToList();
            foreach (var m in lastPlayed)
            {
                teamById.TryGetValue(m.HomeTeamId, out var homeTeam);
                teamById.TryGetValue(m.AwayTeamId, out var awayTeam);
                LastPlayedMatches.Add(CreateMatchRow(m, homeTeam?.Name, awayTeam?.Name, isLive: false));
            }
            var upcoming = matches
                .Where(m => m.Status == MatchStatus.Scheduled || m.Status == MatchStatus.InProgress)
                .OrderBy(m => m.DateTime ?? DateTime.MaxValue)
                .Take(5)
                .ToList();
            foreach (var m in upcoming)
            {
                teamById.TryGetValue(m.HomeTeamId, out var homeTeam);
                teamById.TryGetValue(m.AwayTeamId, out var awayTeam);
                UpcomingMatches.Add(CreateMatchRow(m, homeTeam?.Name, awayTeam?.Name, m.Status == MatchStatus.InProgress));
            }

            Stages.Clear();
            var stages = await _stageRepository.GetByTournamentAsync(tournamentId);
            foreach (var s in stages)
                Stages.Add(s);
            _stageSelectionVersion++;
            SelectedStage = null;

            ParticipantTeams.Clear();
            var sortedParticipants = teams
                .OrderBy(t => t.Name, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true))
                .ToList();
            foreach (var t in sortedParticipants)
                ParticipantTeams.Add(t);
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    private void RefreshStageMatches()
    {
        _ = RefreshStageMatchesAsync();
    }

    private async Task RefreshStageMatchesAsync()
    {
        var myVersion = ++_stageSelectionVersion;
        try
        {
            if (Tournament is null || SelectedStage is null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StageMatches.Clear();
                    StandingsByGroupForStage.Clear();
                    StageZoneLegend.Clear();
                    ShowStageZoneLegend = false;
                });
                return;
            }

            // Сразу очищаем UI при переключении стадии, чтобы не было "мигания"
            // данных от предыдущей стадии, пока идет фоновая загрузка.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StageMatches.Clear();
                StandingsByGroupForStage.Clear();
                StageZoneLegend.Clear();
                ShowStageZoneLegend = false;
            });

            var stageId = SelectedStage.Id;
            var matches = await _matchRepository.GetByTournamentAsync(Tournament.Id);
            var stageMatches = matches
                .Where(m => m.StageId == stageId)
                .OrderByDescending(m => m.DateTime ?? DateTime.MinValue)
                .ToList();
            var allTeams = await _teamRepository.GetByTournamentAsync(Tournament.Id);
            var teamById = allTeams.ToDictionary(t => t.Id);

            // Состав стадии: ограничиваем команды для таблицы только теми, которые добавлены в StageTeams.
            var stageTeamIds = (await _stageTeamRepository.GetTeamIdsByStageAsync(stageId)).ToHashSet();
            if (stageTeamIds.Count == 0 && stageMatches.Count > 0)
            {
                // Backward-compatibility: если StageTeams пустая, но матчи уже есть — извлекаем команды из матчей.
                var fromMatches = stageMatches
                    .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
                    .Distinct()
                    .ToList();

                var teamGroupByTeamId = fromMatches.ToDictionary(
                    id => id,
                    id => teamById.TryGetValue(id, out var t) ? t.GroupId : null);

                stageTeamIds = fromMatches.ToHashSet();
                await _stageTeamRepository.AddTeamsToStageAsync(stageId, teamGroupByTeamId);
            }

            var teamsInStage = allTeams.Where(t => stageTeamIds.Contains(t.Id)).ToList();
            var teamGroupIdsInStage = await _stageTeamRepository.GetTeamGroupIdsByStageAsync(stageId);

            // Группы управляются внутри стадии.
            var stageGroups = (await _stageGroupRepository.GetByStageAsync(stageId)).ToList();
            if (stageGroups.Count == 0 && Tournament.Rules?.Groups is { Count: > 0 } globalGroups)
            {
                foreach (var g in globalGroups.OrderBy(x => x.Order).ThenBy(x => x.Name))
                    await _stageGroupRepository.AddGroupAsync(stageId, g.Name);

                stageGroups = (await _stageGroupRepository.GetByStageAsync(stageId)).ToList();
            }

            var rows = new List<MatchRow>();
            foreach (var m in stageMatches)
            {
                teamById.TryGetValue(m.HomeTeamId, out var homeTeam);
                teamById.TryGetValue(m.AwayTeamId, out var awayTeam);
                rows.Add(CreateMatchRow(m, homeTeam?.Name, awayTeam?.Name, m.Status == MatchStatus.InProgress));
            }

            IReadOnlyDictionary<Guid, Guid?>? teamZoneIds = null;
            IReadOnlyDictionary<Guid, string>? zoneColorById = null;
            IReadOnlyList<StageColorZone>? swissZonesForLegend = null;
            if (SelectedStage is { StageType: StageType.Swiss })
            {
                var zones = await _stageColorZoneRepository.GetZonesByStageAsync(stageId);
                swissZonesForLegend = zones;
                if (zones.Count > 0)
                {
                    zoneColorById = zones.ToDictionary(z => z.Id, z => z.ColorHex);
                    var assign = await _stageColorZoneRepository.GetTeamZoneAssignmentsAsync(stageId);
                    teamZoneIds = assign.ToDictionary(kv => kv.Key, kv => (Guid?)kv.Value);
                }
            }

            var barW = SelectedStage is { StageType: StageType.Swiss } && swissZonesForLegend is { Count: > 0 } ? 6 : 0;
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

            if (myVersion != _stageSelectionVersion)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                StageMatches.Clear();
                foreach (var r in rows)
                    StageMatches.Add(r);
                StandingsByGroupForStage.Clear();
                foreach (var g in stageStandingsGroups)
                    StandingsByGroupForStage.Add(g);

                StageZoneLegend.Clear();
                if (swissZonesForLegend is not null)
                {
                    foreach (var z in swissZonesForLegend
                                 .OrderBy(x => x.SortOrder)
                                 .ThenBy(x => x.Name, StringComparer.Ordinal))
                        StageZoneLegend.Add(new StageZoneLegendItem(z.Name, z.ColorHex));
                }
                ShowStageZoneLegend = swissZonesForLegend is { Count: > 0 };
            });
        }
        catch
        {
            if (myVersion != _stageSelectionVersion)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                StageMatches.Clear();
                StandingsByGroupForStage.Clear();
                StageZoneLegend.Clear();
                ShowStageZoneLegend = false;
            });
        }
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

    public async Task SetMatchStatusAsync(Guid matchId, MatchStatus status)
    {
        var matches = await _matchRepository.GetByTournamentAsync(Tournament!.Id);
        var match = matches.FirstOrDefault(m => m.Id == matchId);
        if (match is null) return;
        match.Status = status;
        await _matchRepository.SaveAsync(match);
        await LoadAsync(Tournament.Id);
    }

    public async Task DeleteParticipantAsync(Guid teamId)
    {
        if (Tournament is null) return;
        await _teamRepository.DeleteAsync(teamId);
        await LoadAsync(Tournament.Id);
    }

    public async Task DeleteStageAsync(Guid stageId)
    {
        if (Tournament is null) return;
        await _stageRepository.DeleteAsync(stageId);
        if (SelectedStage?.Id == stageId)
            SelectedStage = null;
        await LoadAsync(Tournament.Id);
    }

    private void UpdateRulesDisplay()
    {
        var r = Tournament?.Rules;
        if (r is null)
        {
            PointsRegWinText = PointsOtWinText = PointsSoWinText = "—";
            PointsRegLossText = PointsOtLossText = PointsSoLossText = "—";
        }
        else
        {
            PointsRegWinText = r.PointsForRegulationWin.ToString();
            PointsOtWinText = r.PointsForOvertimeWin.ToString();
            PointsSoWinText = r.PointsForShootoutWin.ToString();
            PointsRegLossText = r.PointsForRegulationLoss.ToString();
            PointsOtLossText = r.PointsForOvertimeLoss.ToString();
            PointsSoLossText = r.PointsForShootoutLoss.ToString();
        }
    }

    private void UpdateSortOrderDisplay()
    {
        var order = Tournament?.Rules?.SortOrder is { Count: > 0 } sortOrder
            ? sortOrder
            : TournamentRules.GetDefaultSortOrder();
        SortOrderDisplay = order;
        OnPropertyChanged(nameof(SortOrderDisplay));
    }

    /// <summary>
    /// Returns list of 5 values: 0 = empty, 1 = win, 2 = loss. Order: most recent first (index 0 = latest).
    /// </summary>
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

    /// <summary>
    /// Format percentage: round to hundredths, no trailing zeros (e.g. 34.56 or 34.5).
    /// </summary>
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
            OutcomeType.Shootout => hasShootoutPeriod ? "" : match.ShootoutScoreHome is { } sh && match.ShootoutScoreAway is { } sa
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

/// <summary>Группа строк турнирной таблицы (для отображения с заголовком группы).</summary>
public sealed class StandingGroup : ObservableCollection<StandingRow>
{
    public string GroupName { get; set; } = string.Empty;
}

public sealed class StandingRow
{
    public Guid TeamId { get; set; }
    /// <summary>Назначенная цветовая зона для строки стадии; null — без зоны.</summary>
    public Guid? ZoneId { get; set; }
    public int Place { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string? TeamIconPath { get; set; }
    /// <summary>Цвет полосы зоны слева в таблице стадии (швейцарская); null — без зоны.</summary>
    public string? ZoneBarColorHex { get; set; }
    /// <summary>Ширина колонки полосы (0 — не показывать колонку).</summary>
    public int ZoneBarColumnWidth { get; set; }
    /// <summary>Полоса только если есть хотя бы одна зона и у команды назначен цвет.</summary>
    public bool ShowZoneBarStripe { get; set; }
    public int Games { get; set; }
    public int WinsReg { get; set; }
    public int WinsOt { get; set; }
    public int WinsSo { get; set; }
    public int LossesReg { get; set; }
    public int LossesOt { get; set; }
    public int LossesSo { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDiff { get; set; }
    /// <summary>5 items: 0 = empty, 1 = win, 2 = loss. Index 0 = most recent.</summary>
    public IReadOnlyList<int> Last5Results { get; set; } = Array.Empty<int>();
    public string PointsPct { get; set; } = "0";
    public int Points { get; set; }
}

public sealed class MatchRow
{
    public Guid MatchId { get; set; }
    public Guid? SeriesId { get; set; }
    public DateTime? DateTime { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public string DisplayScore { get; set; } = string.Empty;
    public bool IsLive { get; set; }
    public MatchStatus Status { get; set; }
}

public sealed class CompetitionStatsUi
{
    public string FinishedMatches { get; set; } = "0";
    public string WinsRegulation { get; set; } = "0";
    public string WinsOvertime { get; set; } = "0";
    public string WinsShootout { get; set; } = "0";
    public string LossesRegulation { get; set; } = "0";
    public string LossesOvertime { get; set; } = "0";
    public string LossesShootout { get; set; } = "0";
    public string GoalsFor { get; set; } = "0";
    public string GoalsAgainst { get; set; } = "0";
    public string Period1 { get; set; } = "0-0-0";
    public string Period2 { get; set; } = "0-0-0";
    public string Period3 { get; set; } = "0-0-0";
    public string Overtime { get; set; } = "0-0-0";
    public string Shootout { get; set; } = "0-0-0";

    public static CompetitionStatsUi FromDomain(CompetitionStats stats) =>
        new()
        {
            FinishedMatches = stats.FinishedMatches.ToString(),
            WinsRegulation = stats.WinsRegulation.ToString(),
            WinsOvertime = stats.WinsOvertime.ToString(),
            WinsShootout = stats.WinsShootout.ToString(),
            LossesRegulation = stats.LossesRegulation.ToString(),
            LossesOvertime = stats.LossesOvertime.ToString(),
            LossesShootout = stats.LossesShootout.ToString(),
            GoalsFor = stats.GoalsFor.ToString(),
            GoalsAgainst = stats.GoalsAgainst.ToString(),
            Period1 = $"{stats.Period1.Wins}-{stats.Period1.Draws}-{stats.Period1.Losses}",
            Period2 = $"{stats.Period2.Wins}-{stats.Period2.Draws}-{stats.Period2.Losses}",
            Period3 = $"{stats.Period3.Wins}-{stats.Period3.Draws}-{stats.Period3.Losses}",
            Overtime = $"{stats.Overtime.Wins}-{stats.Overtime.Draws}-{stats.Overtime.Losses}",
            Shootout = $"{stats.Shootout.Wins}-{stats.Shootout.Draws}-{stats.Shootout.Losses}"
        };
}

