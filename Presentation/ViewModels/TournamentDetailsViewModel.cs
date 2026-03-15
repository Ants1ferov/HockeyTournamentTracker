using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class TournamentDetailsViewModel : INotifyPropertyChanged
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly StatsService _statsService;

    private Tournament? _tournament;
    private string _pointsRegWinText = "—";
    private string _pointsOtWinText = "—";
    private string _pointsSoWinText = "—";
    private string _pointsRegLossText = "—";
    private string _pointsOtLossText = "—";
    private string _pointsSoLossText = "—";

    public Tournament? Tournament
    {
        get => _tournament;
        private set
        {
            if (SetField(ref _tournament, value))
            {
                OnPropertyChanged(nameof(StatusIndex));
                UpdateRulesDisplay();
                UpdateSortOrderDisplay();
            }
        }
    }

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

    /// <summary>Порядок распределения мест для отображения на экране деталей турнира.</summary>
    public IReadOnlyList<StandingSortCriterion> SortOrderDisplay { get; private set; } = Array.Empty<StandingSortCriterion>();

    public TournamentDetailsViewModel(
        ITournamentRepository tournamentRepository,
        ITeamRepository teamRepository,
        IMatchRepository matchRepository,
        StatsService statsService)
    {
        _tournamentRepository = tournamentRepository;
        _teamRepository = teamRepository;
        _matchRepository = matchRepository;
        _statsService = statsService;
    }

    public async Task LoadAsync(Guid tournamentId)
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

    public async Task SetMatchStatusAsync(Guid matchId, MatchStatus status)
    {
        var matches = await _matchRepository.GetByTournamentAsync(Tournament!.Id);
        var match = matches.FirstOrDefault(m => m.Id == matchId);
        if (match is null) return;
        match.Status = status;
        await _matchRepository.SaveAsync(match);
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

/// <summary>Группа строк турнирной таблицы (для отображения с заголовком группы).</summary>
public sealed class StandingGroup : ObservableCollection<StandingRow>
{
    public string GroupName { get; set; } = string.Empty;
}

public sealed class StandingRow
{
    public int Place { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string? TeamIconPath { get; set; }
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
    public DateTime? DateTime { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public string DisplayScore { get; set; } = string.Empty;
    public bool IsLive { get; set; }
    public MatchStatus Status { get; set; }
}

