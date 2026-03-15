using System.Collections.ObjectModel;
using System.ComponentModel;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class TournamentsListViewModel : INotifyPropertyChanged
{
    private readonly ITournamentRepository _tournamentRepository;
    private bool _isBusy;

    public ObservableCollection<Tournament> Tournaments { get; } = new();

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

    public TournamentsListViewModel(ITournamentRepository tournamentRepository)
    {
        _tournamentRepository = tournamentRepository;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            Tournaments.Clear();
            var items = await _tournamentRepository.GetAllAsync();
            foreach (var item in items.OrderByDescending(t => t.StartDate ?? DateTime.MinValue))
            {
                Tournaments.Add(item);
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
