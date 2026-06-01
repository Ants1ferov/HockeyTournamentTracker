using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

[QueryProperty(nameof(LeagueIdStr), "LeagueId")]
public sealed class LeagueEditViewModel : INotifyPropertyChanged
{
    private readonly ILeagueRepository _leagueRepository;
    private readonly ITournamentRepository _tournamentRepository;

    private Guid _leagueId;
    private string? _leagueIdStr;
    private string _name = string.Empty;
    private string? _description;
    private string? _sport;
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

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string? Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public string? Sport
    {
        get => _sport;
        set { _sport = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); }
    }

    public ObservableCollection<TournamentSelectionItem> TournamentItems { get; } = new();

    public LeagueEditViewModel(ILeagueRepository leagueRepository, ITournamentRepository tournamentRepository)
    {
        _leagueRepository = leagueRepository;
        _tournamentRepository = tournamentRepository;
    }

    private async Task LoadAsync(Guid leagueId)
    {
        if (IsBusy) return;
        IsBusy = true;
        _leagueId = leagueId;
        try
        {
            var league = await _leagueRepository.GetByIdAsync(leagueId);
            if (league is not null)
            {
                Name = league.Name;
                Description = league.Description;
                Sport = league.Sport;
            }

            await LoadTournamentsAsync(leagueId);
        }
        finally { IsBusy = false; }
    }

    public async Task LoadTournamentsAsync(Guid? forLeagueId = null)
    {
        var all = await _tournamentRepository.GetAllAsync();
        TournamentItems.Clear();
        foreach (var t in all.OrderBy(t => t.Name))
        {
            TournamentItems.Add(new TournamentSelectionItem
            {
                Tournament = t,
                IsSelected = t.LeagueId.HasValue && t.LeagueId == (forLeagueId ?? _leagueId)
            });
        }
    }

    public async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) return false;

        if (_leagueId == Guid.Empty)
            _leagueId = Guid.NewGuid();

        var league = new League
        {
            Id = _leagueId,
            Name = Name.Trim(),
            Description = Description,
            Sport = string.IsNullOrWhiteSpace(Sport) ? null : Sport.Trim()
        };
        await _leagueRepository.SaveAsync(league);

        foreach (var item in TournamentItems)
        {
            if (item.IsSelected)
                await _tournamentRepository.SetLeagueAsync(item.Tournament.Id, _leagueId);
            else if (item.Tournament.LeagueId == _leagueId)
                await _tournamentRepository.SetLeagueAsync(item.Tournament.Id, null);
        }

        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class TournamentSelectionItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public Tournament Tournament { get; set; } = null!;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
