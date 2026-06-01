using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

[QueryProperty(nameof(LeagueIdStr), "LeagueId")]
public sealed class LeagueDetailsViewModel : INotifyPropertyChanged
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ITournamentRepository _tournamentRepository;

    private string? _leagueIdStr;
    private League? _league;
    private bool _isBusy;

    public string? LeagueIdStr
    {
        get => _leagueIdStr;
        set
        {
            _leagueIdStr = value;
            if (Guid.TryParse(value, out var id))
                _ = LoadAsync(id);
        }
    }

    public League? League
    {
        get => _league;
        private set { _league = value; OnPropertyChanged(); OnPropertyChanged(nameof(LeagueName)); }
    }

    public string LeagueName => _league?.Name ?? string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); }
    }

    public ObservableCollection<Tournament> Tournaments { get; } = new();

    public LeagueDetailsViewModel(ILeagueRepository leagueRepository, ITournamentRepository tournamentRepository)
    {
        _leagueRepository = leagueRepository;
        _tournamentRepository = tournamentRepository;
    }

    public async Task LoadAsync(Guid leagueId)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            League = await _leagueRepository.GetByIdAsync(leagueId);
            Tournaments.Clear();
            var items = await _tournamentRepository.GetByLeagueAsync(leagueId);
            foreach (var t in items.OrderByDescending(t => t.StartDate ?? DateTime.MinValue))
                Tournaments.Add(t);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
