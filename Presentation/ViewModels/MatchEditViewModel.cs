using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class MatchEditViewModel : INotifyPropertyChanged
{
    private readonly ITeamRepository _teamRepository;
    private readonly IMatchRepository _matchRepository;

    private Guid _tournamentId;
    private Team? _homeTeam;
    private Team? _awayTeam;
    private DateTime _date = DateTime.Today;
    private TimeSpan _time = DateTime.Now.TimeOfDay;
    private int _homeGoals;
    private int _awayGoals;
    private OutcomeType _outcomeType = OutcomeType.Regulation;
    private int _shootoutHome;
    private int _shootoutAway;

    public ObservableCollection<Team> Teams { get; } = new();

    public Guid TournamentId
    {
        get => _tournamentId;
        set => SetField(ref _tournamentId, value);
    }

    public Team? HomeTeam
    {
        get => _homeTeam;
        set => SetField(ref _homeTeam, value);
    }

    public Team? AwayTeam
    {
        get => _awayTeam;
        set => SetField(ref _awayTeam, value);
    }

    public DateTime Date
    {
        get => _date;
        set => SetField(ref _date, value);
    }

    public TimeSpan Time
    {
        get => _time;
        set => SetField(ref _time, value);
    }

    public int HomeGoals
    {
        get => _homeGoals;
        set => SetField(ref _homeGoals, value);
    }

    public int AwayGoals
    {
        get => _awayGoals;
        set => SetField(ref _awayGoals, value);
    }

    public OutcomeType OutcomeType
    {
        get => _outcomeType;
        set => SetField(ref _outcomeType, value);
    }

    public int ShootoutHome
    {
        get => _shootoutHome;
        set => SetField(ref _shootoutHome, value);
    }

    public int ShootoutAway
    {
        get => _shootoutAway;
        set => SetField(ref _shootoutAway, value);
    }

    public MatchEditViewModel(ITeamRepository teamRepository, IMatchRepository matchRepository)
    {
        _teamRepository = teamRepository;
        _matchRepository = matchRepository;
    }

    public async Task LoadTeamsAsync()
    {
        Teams.Clear();
        if (TournamentId == Guid.Empty) return;

        var teams = await _teamRepository.GetByTournamentAsync(TournamentId);
        foreach (var team in teams)
        {
            Teams.Add(team);
        }
    }

    public async Task<bool> SaveAsync()
    {
        if (TournamentId == Guid.Empty || HomeTeam is null || AwayTeam is null || HomeTeam.Id == AwayTeam.Id)
            return false;

        var dateTime = Date.Date + Time;

        var match = new Match
        {
            TournamentId = TournamentId,
            DateTime = dateTime,
            HomeTeamId = HomeTeam.Id,
            AwayTeamId = AwayTeam.Id,
            HomeGoals = HomeGoals,
            AwayGoals = AwayGoals,
            OutcomeType = OutcomeType,
            ShootoutScoreHome = OutcomeType == OutcomeType.Shootout ? ShootoutHome : null,
            ShootoutScoreAway = OutcomeType == OutcomeType.Shootout ? ShootoutAway : null,
            Status = MatchStatus.Finished
        };

        await _matchRepository.SaveAsync(match);
        return true;
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

