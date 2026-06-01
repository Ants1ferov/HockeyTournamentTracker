using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

/// <summary>Карточка турнира внутри лиги или в секции «без лиги» (экран 1).</summary>
public sealed class TournamentCardVm
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public TournamentStatus Status { get; init; }
    /// <summary>Краткая метка сезона для квадрата, например «25/26».</summary>
    public string SeasonLabel { get; init; } = string.Empty;
    public int TeamCount { get; init; }
    /// <summary>Строка-подпись: «Идёт · 16 команд» / «Завершён · Локомотив 🏆».</summary>
    public string SubtitleLine { get; init; } = string.Empty;
    public bool IsLive => Status == TournamentStatus.InProgress;
}

public sealed class LeagueCardVm : INotifyPropertyChanged
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? IconPath { get; init; }
    public string? Sport { get; init; }
    public int TournamentCount { get; init; }

    public ObservableCollection<TournamentCardVm> Tournaments { get; } = new();

    /// <summary>«2 турнира · хоккей» (вид спорта добавляется, если задан).</summary>
    public string Subtitle
    {
        get
        {
            var t = $"{TournamentCount} {PluralTournaments(TournamentCount)}";
            return string.IsNullOrWhiteSpace(Sport) ? t : $"{t} · {Sport}";
        }
    }

    /// <summary>Инициалы (до 3 символов) для квадрата-аватара.</summary>
    public string Initial =>
        Name.Length > 0 ? Name[..Math.Min(3, Name.Length)].ToUpperInvariant() : "?";

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged(nameof(IsExpanded));
        }
    }

    private static string PluralTournaments(int n)
    {
        var n100 = n % 100;
        var n10 = n % 10;
        if (n100 is >= 11 and <= 14) return "турниров";
        return n10 switch { 1 => "турнир", >= 2 and <= 4 => "турнира", _ => "турниров" };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class TournamentsListViewModel : INotifyPropertyChanged
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ILeagueRepository _leagueRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly StatsService _statsService;
    private bool _isBusy;

    public ObservableCollection<LeagueCardVm> Leagues { get; } = new();
    public ObservableCollection<TournamentCardVm> TournamentsWithoutLeague { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public TournamentsListViewModel(
        ITournamentRepository tournamentRepository,
        ILeagueRepository leagueRepository,
        ITeamRepository teamRepository,
        IMatchRepository matchRepository,
        StatsService statsService)
    {
        _tournamentRepository = tournamentRepository;
        _leagueRepository = leagueRepository;
        _teamRepository = teamRepository;
        _matchRepository = matchRepository;
        _statsService = statsService;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var tournaments = await _tournamentRepository.GetAllAsync();
            var leagues = await _leagueRepository.GetAllAsync();

            // Построить карточки турниров (число команд + чемпион).
            var cardByTournamentId = new Dictionary<Guid, TournamentCardVm>();
            foreach (var t in tournaments)
                cardByTournamentId[t.Id] = await BuildTournamentCardAsync(t);

            // Сохранить состояние раскрытия лиг между перезагрузками.
            var expandedIds = Leagues.Where(l => l.IsExpanded).Select(l => l.Id).ToHashSet();

            Leagues.Clear();
            foreach (var l in leagues)
            {
                var leagueTournaments = tournaments
                    .Where(t => t.LeagueId == l.Id)
                    .OrderByDescending(t => t.StartDate ?? DateTime.MinValue)
                    .ToList();

                var card = new LeagueCardVm
                {
                    Id = l.Id,
                    Name = l.Name,
                    IconPath = l.IconPath,
                    Sport = l.Sport,
                    TournamentCount = leagueTournaments.Count,
                    IsExpanded = expandedIds.Contains(l.Id)
                };
                foreach (var t in leagueTournaments)
                    card.Tournaments.Add(cardByTournamentId[t.Id]);
                Leagues.Add(card);
            }

            TournamentsWithoutLeague.Clear();
            foreach (var t in tournaments
                         .Where(t => t.LeagueId is null)
                         .OrderByDescending(t => t.StartDate ?? DateTime.MinValue))
                TournamentsWithoutLeague.Add(cardByTournamentId[t.Id]);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<TournamentCardVm> BuildTournamentCardAsync(Tournament t)
    {
        var teams = await _teamRepository.GetByTournamentAsync(t.Id);
        var teamCount = teams.Count;

        string? champion = null;
        if (t.Status == TournamentStatus.Finished && teams.Count > 0)
        {
            var matches = await _matchRepository.GetByTournamentAsync(t.Id);
            var standings = _statsService.CalculateStandings(t, teams, matches);
            var sorted = StatsService.SortByRules(standings, t.Rules ?? new TournamentRules());
            var topId = sorted.FirstOrDefault()?.TeamId;
            if (topId is { } id)
                champion = teams.FirstOrDefault(x => x.Id == id)?.Name;
        }

        return new TournamentCardVm
        {
            Id = t.Id,
            Name = t.Name,
            Status = t.Status,
            SeasonLabel = BuildSeasonLabel(t.StartDate, t.EndDate),
            TeamCount = teamCount,
            SubtitleLine = BuildSubtitle(t.Status, teamCount, champion)
        };
    }

    private static string BuildSubtitle(TournamentStatus status, int teamCount, string? champion) => status switch
    {
        TournamentStatus.InProgress => $"Идёт · {teamCount} команд",
        TournamentStatus.Finished => string.IsNullOrWhiteSpace(champion)
            ? "Завершён"
            : $"Завершён · {champion} 🏆",
        TournamentStatus.Archived => $"В архиве · {teamCount} команд",
        _ => $"Запланирован · {teamCount} команд"
    };

    private static string BuildSeasonLabel(DateTime? start, DateTime? end)
    {
        if (start is null) return string.Empty;
        var s = start.Value.Year % 100;
        if (end is { } e && e.Year != start.Value.Year)
            return $"{s:00}/{e.Year % 100:00}";
        return $"{s:00}";
    }

    public async Task DeleteTournamentAsync(Guid tournamentId)
    {
        await _tournamentRepository.DeleteAsync(tournamentId);
        await LoadAsync();
    }

    public async Task DeleteLeagueAsync(LeagueCardVm league)
    {
        await _leagueRepository.DeleteAsync(league.Id);
        await LoadAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
