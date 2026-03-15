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
        private set => SetField(ref _tournament, value);
    }

    public ObservableCollection<StandingRow> Standings { get; } = new();
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

        Matches.Clear();
        foreach (var m in matches.OrderBy(m => m.DateTime ?? DateTime.MinValue))
        {
            teamById.TryGetValue(m.HomeTeamId, out var homeTeam);
            teamById.TryGetValue(m.AwayTeamId, out var awayTeam);

            Matches.Add(new MatchRow
            {
                DateTime = m.DateTime,
                HomeTeamName = homeTeam?.Name ?? string.Empty,
                AwayTeamName = awayTeam?.Name ?? string.Empty,
                DisplayScore = BuildScoreText(m)
            });
        }
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
    public DateTime? DateTime { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public string DisplayScore { get; set; } = string.Empty;
}

