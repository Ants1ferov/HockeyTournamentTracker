using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class MatchEditViewModel : INotifyPropertyChanged
{
    private readonly ITeamRepository _teamRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IStageTeamRepository _stageTeamRepository;

    private Guid _tournamentId;
    private Guid _matchId;
    private Guid? _stageId;
    private Tournament? _tournament;
    private Team? _homeTeam;
    private Team? _awayTeam;
    private DateTime _date = DateTime.Today;
    private TimeSpan _time = DateTime.Now.TimeOfDay;
    private int _homeGoals;
    private int _awayGoals;
    private OutcomeType _outcomeType = OutcomeType.Regulation;
    private int _shootoutHome;
    private int _shootoutAway;
    private int _p1Home;
    private int _p1Away;
    private int _p2Home;
    private int _p2Away;
    private int _p3Home;
    private int _p3Away;
    private bool _hasOvertime;
    private int _otHome;
    private int _otAway;

    public ObservableCollection<Team> Teams { get; } = new();
    public ObservableCollection<Team> AwayTeamOptions { get; } = new();

    public Guid TournamentId
    {
        get => _tournamentId;
        set => SetField(ref _tournamentId, value);
    }

    public Guid MatchId
    {
        get => _matchId;
        set => SetField(ref _matchId, value);
    }

    public Guid? StageId
    {
        get => _stageId;
        set => SetField(ref _stageId, value);
    }

    public Team? HomeTeam
    {
        get => _homeTeam;
        set
        {
            if (SetField(ref _homeTeam, value))
                _ = RefreshAwayTeamOptionsAsync();
        }
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
        set
        {
            if (SetField(ref _outcomeType, value))
            {
                OnPropertyChanged(nameof(OutcomeIndex));
                OnPropertyChanged(nameof(IsShootoutVisible));
            }
        }
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

    public int OutcomeIndex
    {
        get => (int)OutcomeType;
        set
        {
            var newVal = (OutcomeType)Math.Clamp(value, 0, 2);
            if (SetField(ref _outcomeType, newVal))
                OnPropertyChanged(nameof(IsShootoutVisible));
        }
    }

    public bool IsShootoutVisible => OutcomeType == OutcomeType.Shootout;

    public int P1Home { get => _p1Home; set => SetField(ref _p1Home, value); }
    public int P1Away { get => _p1Away; set => SetField(ref _p1Away, value); }
    public int P2Home { get => _p2Home; set => SetField(ref _p2Home, value); }
    public int P2Away { get => _p2Away; set => SetField(ref _p2Away, value); }
    public int P3Home { get => _p3Home; set => SetField(ref _p3Home, value); }
    public int P3Away { get => _p3Away; set => SetField(ref _p3Away, value); }
    public bool HasOvertime { get => _hasOvertime; set => SetField(ref _hasOvertime, value); }
    public int OTHome { get => _otHome; set => SetField(ref _otHome, value); }
    public int OTAway { get => _otAway; set => SetField(ref _otAway, value); }

    public MatchEditViewModel(
        ITeamRepository teamRepository,
        IMatchRepository matchRepository,
        ITournamentRepository tournamentRepository,
        IStageTeamRepository stageTeamRepository)
    {
        _teamRepository = teamRepository;
        _matchRepository = matchRepository;
        _tournamentRepository = tournamentRepository;
        _stageTeamRepository = stageTeamRepository;
    }

    private async Task RefreshAwayTeamOptionsAsync()
    {
        AwayTeamOptions.Clear();
        if (_tournament?.Rules.AllowCrossGroupMatches == true || HomeTeam is null || StageId is null || StageId == Guid.Empty)
        {
            foreach (var t in Teams)
                AwayTeamOptions.Add(t);
            return;
        }

        var stageId = StageId.Value;
        var teamGroupIdsInStage = await _stageTeamRepository.GetTeamGroupIdsByStageAsync(stageId);
        var homeGroupId = teamGroupIdsInStage.TryGetValue(HomeTeam.Id, out var gid) ? gid : null;

        foreach (var t in Teams)
        {
            var tidGroupId = teamGroupIdsInStage.TryGetValue(t.Id, out var teamGid) ? teamGid : null;
            if (tidGroupId == homeGroupId)
                AwayTeamOptions.Add(t);
        }
    }

    public async Task LoadTeamsAsync()
    {
        Teams.Clear();
        AwayTeamOptions.Clear();
        if (TournamentId == Guid.Empty) return;

        _tournament = await _tournamentRepository.GetByIdAsync(TournamentId);
        var allTeams = await _teamRepository.GetByTournamentAsync(TournamentId);

        if (StageId is { } stageId && stageId != Guid.Empty)
        {
            var stageTeamIds = await _stageTeamRepository.GetTeamIdsByStageAsync(stageId);
            if (stageTeamIds.Count == 0)
            {
                // Backward-compatibility: если StageTeams пустая, а матчи уже есть — извлекаем команды из матчей.
                var matches = await _matchRepository.GetByTournamentAsync(TournamentId);
                var stageMatches = matches.Where(m => m.StageId == stageId).ToList();
                var derivedTeamIds = stageMatches
                    .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
                    .Distinct()
                    .ToList();

                if (derivedTeamIds.Count > 0)
                {
                    var teamGroupByTeamId = derivedTeamIds.ToDictionary(
                        id => id,
                        id => allTeams.FirstOrDefault(t => t.Id == id)?.GroupId);
                    await _stageTeamRepository.AddTeamsToStageAsync(stageId, teamGroupByTeamId);
                }

                stageTeamIds = derivedTeamIds;
            }

            var stageTeamIdSet = stageTeamIds.ToHashSet();
            foreach (var team in allTeams.Where(t => stageTeamIdSet.Contains(t.Id)))
                Teams.Add(team);
        }
        else
        {
            foreach (var team in allTeams)
                Teams.Add(team);
        }
        await RefreshAwayTeamOptionsAsync();
    }

    public async Task LoadMatchAsync()
    {
        if (MatchId == Guid.Empty) return;
        var match = await _matchRepository.GetByIdAsync(MatchId);
        if (match is null) return;

        StageId = match.StageId;
        await LoadTeamsAsync();
        HomeTeam = Teams.FirstOrDefault(t => t.Id == match.HomeTeamId);
        AwayTeam = AwayTeamOptions.FirstOrDefault(t => t.Id == match.AwayTeamId) ?? Teams.FirstOrDefault(t => t.Id == match.AwayTeamId);
        if (match.DateTime.HasValue)
        {
            Date = match.DateTime.Value.Date;
            Time = match.DateTime.Value.TimeOfDay;
        }
        HomeGoals = match.HomeGoals ?? 0;
        AwayGoals = match.AwayGoals ?? 0;
        OutcomeType = match.OutcomeType ?? OutcomeType.Regulation;
        ShootoutHome = match.ShootoutScoreHome ?? 0;
        ShootoutAway = match.ShootoutScoreAway ?? 0;
        if (match.PeriodScores is { Count: > 0 } periods)
        {
            var ordered = periods.OrderBy(p => p.PeriodNumber).ToList();

            P1Home = ordered.FirstOrDefault(p => p.PeriodType == PeriodType.Regular && p.PeriodNumber == 1)?.HomeGoals ?? 0;
            P1Away = ordered.FirstOrDefault(p => p.PeriodType == PeriodType.Regular && p.PeriodNumber == 1)?.AwayGoals ?? 0;

            P2Home = ordered.FirstOrDefault(p => p.PeriodType == PeriodType.Regular && p.PeriodNumber == 2)?.HomeGoals ?? 0;
            P2Away = ordered.FirstOrDefault(p => p.PeriodType == PeriodType.Regular && p.PeriodNumber == 2)?.AwayGoals ?? 0;

            P3Home = ordered.FirstOrDefault(p => p.PeriodType == PeriodType.Regular && p.PeriodNumber == 3)?.HomeGoals ?? 0;
            P3Away = ordered.FirstOrDefault(p => p.PeriodType == PeriodType.Regular && p.PeriodNumber == 3)?.AwayGoals ?? 0;

            var ot = ordered.FirstOrDefault(p => p.PeriodType == PeriodType.Overtime);
            HasOvertime = ot is not null;
            OTHome = ot?.HomeGoals ?? 0;
            OTAway = ot?.AwayGoals ?? 0;

            var shootout = ordered.FirstOrDefault(p => p.PeriodType == PeriodType.Shootout);
            if (shootout is not null)
            {
                ShootoutHome = shootout.HomeGoals;
                ShootoutAway = shootout.AwayGoals;
                // Если период буллитов есть в данных — показываем UI "по буллитам".
                OutcomeType = OutcomeType.Shootout;
            }
        }
        else
        {
            P1Home = match.HomeGoals ?? 0;
            P1Away = match.AwayGoals ?? 0;
        }
    }

    public async Task<bool> SaveAsync()
    {
        if (TournamentId == Guid.Empty || HomeTeam is null || AwayTeam is null || HomeTeam.Id == AwayTeam.Id)
            return false;

        if (StageId is { } stageId && stageId != Guid.Empty)
        {
            var stageTeamIds = await _stageTeamRepository.GetTeamIdsByStageAsync(stageId);

            if (stageTeamIds.Count == 0)
            {
                // Backward-compatibility: извлекаем команды из матчей, если StageTeams ещё не заполнена.
                var matches = await _matchRepository.GetByTournamentAsync(TournamentId);
                var stageMatches = matches.Where(m => m.StageId == stageId).ToList();
                var derivedTeamIds = stageMatches
                    .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
                    .Distinct()
                    .ToList();

                if (derivedTeamIds.Count > 0)
                {
                    var allTeams = await _teamRepository.GetByTournamentAsync(TournamentId);
                    var teamGroupByTeamId = derivedTeamIds.ToDictionary(
                        id => id,
                        id => allTeams.FirstOrDefault(t => t.Id == id)?.GroupId);

                    await _stageTeamRepository.AddTeamsToStageAsync(stageId, teamGroupByTeamId);
                    var derivedSet = derivedTeamIds.ToHashSet();
                    if (!derivedSet.Contains(HomeTeam.Id) || !derivedSet.Contains(AwayTeam.Id))
                        return false;
                }
            }
            else
            {
                var stageTeamIdSet = stageTeamIds.ToHashSet();
                if (!stageTeamIdSet.Contains(HomeTeam.Id) || !stageTeamIdSet.Contains(AwayTeam.Id))
                    return false;
            }
        }

        if (_tournament?.Rules.AllowCrossGroupMatches == false && StageId is { } validationStageId && validationStageId != Guid.Empty)
        {
            var teamGroupIdsInStage = await _stageTeamRepository.GetTeamGroupIdsByStageAsync(validationStageId);
            var homeGroupId = teamGroupIdsInStage.TryGetValue(HomeTeam.Id, out var hg) ? hg : null;
            var awayGroupId = teamGroupIdsInStage.TryGetValue(AwayTeam.Id, out var ag) ? ag : null;

            if (homeGroupId.HasValue && awayGroupId != homeGroupId)
                return false;
        }

        var dateTime = Date.Date + Time;
        var id = MatchId != Guid.Empty ? MatchId : Guid.NewGuid();

        var periodScores = new List<PeriodScore>
        {
            new() { PeriodNumber = 1, PeriodType = PeriodType.Regular, HomeGoals = P1Home, AwayGoals = P1Away },
            new() { PeriodNumber = 2, PeriodType = PeriodType.Regular, HomeGoals = P2Home, AwayGoals = P2Away },
            new() { PeriodNumber = 3, PeriodType = PeriodType.Regular, HomeGoals = P3Home, AwayGoals = P3Away }
        };
        if (HasOvertime)
            periodScores.Add(new PeriodScore { PeriodNumber = 4, PeriodType = PeriodType.Overtime, HomeGoals = OTHome, AwayGoals = OTAway });

        if (OutcomeType == OutcomeType.Shootout)
        {
            var shootoutPeriodNumber = HasOvertime ? 5 : 4;
            periodScores.Add(new PeriodScore
            {
                PeriodNumber = shootoutPeriodNumber,
                PeriodType = PeriodType.Shootout,
                HomeGoals = ShootoutHome,
                AwayGoals = ShootoutAway
            });
        }

        var totalHome = P1Home + P2Home + P3Home + (HasOvertime ? OTHome : 0);
        var totalAway = P1Away + P2Away + P3Away + (HasOvertime ? OTAway : 0);

        var match = new Match
        {
            Id = id,
            TournamentId = TournamentId,
            StageId = StageId,
            DateTime = dateTime,
            HomeTeamId = HomeTeam.Id,
            AwayTeamId = AwayTeam.Id,
            HomeGoals = periodScores.Count > 0 ? totalHome : HomeGoals,
            AwayGoals = periodScores.Count > 0 ? totalAway : AwayGoals,
            OutcomeType = OutcomeType,
            ShootoutScoreHome = OutcomeType == OutcomeType.Shootout ? ShootoutHome : null,
            ShootoutScoreAway = OutcomeType == OutcomeType.Shootout ? ShootoutAway : null,
            Status = MatchId != Guid.Empty ? MatchStatus.Finished : MatchStatus.Scheduled,
            PeriodScores = periodScores
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

