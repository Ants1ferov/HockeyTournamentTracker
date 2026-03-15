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

    public Tournament? Tournament
    {
        get => _tournament;
        private set
        {
            if (SetField(ref _tournament, value))
                OnPropertyChanged(nameof(StatusIndex));
        }
    }

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
    public ObservableCollection<MatchRow> LiveMatches { get; } = new();
    public ObservableCollection<MatchRow> Matches { get; } = new();

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
        var standings = _statsService.CalculateStandings(Tournament, teams, matches);
        var teamById = teams.ToDictionary(t => t.Id);

        var place = 1;
        foreach (var s in standings)
        {
            if (!teamById.TryGetValue(s.TeamId, out var team))
                continue;

            Standings.Add(new StandingRow
            {
                Place = place++,
                TeamName = team.Name,
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
                Points = s.Points
            });
        }

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

    private static string BuildScoreText(Match match)
    {
        if (match.HomeGoals is null || match.AwayGoals is null || match.OutcomeType is null)
        {
            return "— : —";
        }

        var baseScore = $"{match.HomeGoals}:{match.AwayGoals}";
        return match.OutcomeType switch
        {
            OutcomeType.Overtime => $"{baseScore} ОТ",
            OutcomeType.Shootout => match.ShootoutScoreHome is { } sh && match.ShootoutScoreAway is { } sa
                ? $"{baseScore} Б ({sh}:{sa})"
                : $"{baseScore} Б",
            _ => baseScore
        };
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

public sealed class StandingRow
{
    public int Place { get; set; }
    public string TeamName { get; set; } = string.Empty;
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

