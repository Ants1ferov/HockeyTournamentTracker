using System.Collections.ObjectModel;
using System.ComponentModel;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class LeagueCardVm
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? IconPath { get; init; }
    public int TournamentCount { get; init; }
    public string TournamentCountLabel => $"{TournamentCount} турниров";
    public string Initial => Name.Length > 0 ? Name[0].ToString().ToUpperInvariant() : "?";
}

public sealed class TournamentsListViewModel : INotifyPropertyChanged
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ILeagueRepository _leagueRepository;
    private bool _isBusy;

    public ObservableCollection<Tournament> Tournaments { get; } = new();
    public ObservableCollection<LeagueCardVm> Leagues { get; } = new();

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

    public TournamentsListViewModel(ITournamentRepository tournamentRepository, ILeagueRepository leagueRepository)
    {
        _tournamentRepository = tournamentRepository;
        _leagueRepository = leagueRepository;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var tournaments = await _tournamentRepository.GetAllAsync();
            var leagues = await _leagueRepository.GetAllAsync();

            Tournaments.Clear();
            foreach (var t in tournaments.OrderByDescending(t => t.StartDate ?? DateTime.MinValue))
                Tournaments.Add(t);

            Leagues.Clear();
            foreach (var l in leagues)
            {
                var count = tournaments.Count(t => t.LeagueId == l.Id);
                Leagues.Add(new LeagueCardVm
                {
                    Id = l.Id,
                    Name = l.Name,
                    IconPath = l.IconPath,
                    TournamentCount = count
                });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DeleteTournamentAsync(Tournament tournament)
    {
        await _tournamentRepository.DeleteAsync(tournament.Id);
        Tournaments.Remove(tournament);
    }

    public async Task DeleteLeagueAsync(LeagueCardVm league)
    {
        await _leagueRepository.DeleteAsync(league.Id);
        Leagues.Remove(league);
        // обновить счётчики в лигах не нужно, но турниры могли поменять LeagueId —
        // перезагружаем список турниров чтобы они остались видимы
        var tournaments = await _tournamentRepository.GetAllAsync();
        Tournaments.Clear();
        foreach (var t in tournaments.OrderByDescending(t => t.StartDate ?? DateTime.MinValue))
            Tournaments.Add(t);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
