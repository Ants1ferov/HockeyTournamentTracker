using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.Services;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class MatchEditViewModel : INotifyPropertyChanged
{
    private readonly ITeamRepository _teamRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IStageTeamRepository _stageTeamRepository;
    private readonly StatsService _statsService;
    private readonly IMatchUpdatesNotifier _matchUpdatesNotifier;

    private Guid _tournamentId;
    private Guid _matchId;
    private Guid? _stageId;
    private Guid? _seriesId;
    private Tournament? _tournament;
    private MatchStatus _matchStatus = MatchStatus.Scheduled;
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
    private HeadToHeadStats _headToHead = new();

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

    public Guid? SeriesId
    {
        get => _seriesId;
        set => SetField(ref _seriesId, value);
    }

    public Team? HomeTeam
    {
        get => _homeTeam;
        set
        {
            if (SetField(ref _homeTeam, value))
            {
                _ = RefreshAwayTeamOptionsAsync();
                _ = RefreshHeadToHeadAsync();
            }
        }
    }

    public Team? AwayTeam
    {
        get => _awayTeam;
        set
        {
            if (SetField(ref _awayTeam, value))
                _ = RefreshHeadToHeadAsync();
        }
    }

    public DateTime Date
    {
        get => _date;
        set { if (SetField(ref _date, value)) OnPropertyChanged(nameof(HeaderDateText)); }
    }

    public TimeSpan Time
    {
        get => _time;
        set { if (SetField(ref _time, value)) OnPropertyChanged(nameof(HeaderDateText)); }
    }

    public int HomeGoals
    {
        get => _homeGoals;
        set { if (SetField(ref _homeGoals, value)) RaiseScoreboardProps(); }
    }

    public int AwayGoals
    {
        get => _awayGoals;
        set { if (SetField(ref _awayGoals, value)) RaiseScoreboardProps(); }
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
                RaiseScoreboardProps();
            }
        }
    }

    public int ShootoutHome
    {
        get => _shootoutHome;
        set { if (SetField(ref _shootoutHome, value)) RaiseScoreboardProps(); }
    }

    public int ShootoutAway
    {
        get => _shootoutAway;
        set { if (SetField(ref _shootoutAway, value)) RaiseScoreboardProps(); }
    }

    public int OutcomeIndex
    {
        get => (int)OutcomeType;
        set
        {
            var newVal = (OutcomeType)Math.Clamp(value, 0, 2);
            if (SetField(ref _outcomeType, newVal))
            {
                OnPropertyChanged(nameof(IsShootoutVisible));
                RaiseScoreboardProps();
            }
        }
    }

    public bool IsShootoutVisible => OutcomeType == OutcomeType.Shootout;
    public bool IsScoreVisible => SelectedMatchStatus != MatchStatus.Scheduled;
    public bool IsPeriodScoresVisible => SelectedMatchStatus != MatchStatus.Scheduled;
    public bool IsFinishedDetailsVisible => SelectedMatchStatus == MatchStatus.Finished;

    public MatchStatus SelectedMatchStatus
    {
        get => _matchStatus;
        set
        {
            if (SetField(ref _matchStatus, value))
            {
                OnPropertyChanged(nameof(MatchStatusIndex));
                OnPropertyChanged(nameof(IsScoreVisible));
                OnPropertyChanged(nameof(IsPeriodScoresVisible));
                OnPropertyChanged(nameof(IsFinishedDetailsVisible));
                RaiseScoreboardProps();
            }
        }
    }

    public int MatchStatusIndex
    {
        get => SelectedMatchStatus switch
        {
            MatchStatus.Scheduled => 0,
            MatchStatus.InProgress => 1,
            MatchStatus.Finished => 2,
            _ => 0
        };
        set
        {
            SelectedMatchStatus = value switch
            {
                1 => MatchStatus.InProgress,
                2 => MatchStatus.Finished,
                _ => MatchStatus.Scheduled
            };
        }
    }

    public int P1Home { get => _p1Home; set => SetField(ref _p1Home, value); }
    public int P1Away { get => _p1Away; set => SetField(ref _p1Away, value); }
    public int P2Home { get => _p2Home; set => SetField(ref _p2Home, value); }
    public int P2Away { get => _p2Away; set => SetField(ref _p2Away, value); }
    public int P3Home { get => _p3Home; set => SetField(ref _p3Home, value); }
    public int P3Away { get => _p3Away; set => SetField(ref _p3Away, value); }
    public bool HasOvertime { get => _hasOvertime; set => SetField(ref _hasOvertime, value); }
    public int OTHome { get => _otHome; set => SetField(ref _otHome, value); }
    public int OTAway { get => _otAway; set => SetField(ref _otAway, value); }
    public HeadToHeadStats HeadToHead
    {
        get => _headToHead;
        private set => SetField(ref _headToHead, value);
    }
    public bool HasHeadToHead => HeadToHead.Matches > 0;

    // --- Производные свойства «табло» (шапка страницы матча) ---

    /// <summary>Название турнира для подзаголовка табло.</summary>
    public string CompetitionTitle => _tournament?.Name ?? string.Empty;

    /// <summary>Дата+время матча для подзаголовка табло.</summary>
    public string HeaderDateText => (Date.Date + Time).ToString("dd.MM.yyyy · HH:mm");

    /// <summary>Матч завершён (есть итоговый результат).</summary>
    public bool HasResult => SelectedMatchStatus == MatchStatus.Finished;

    /// <summary>Маркер исхода под счётом табло: «ОТ»/«Б»/«».</summary>
    public string ScoreSuffix => HasResult
        ? OutcomeType switch { OutcomeType.Overtime => "ОТ", OutcomeType.Shootout => "Б", _ => string.Empty }
        : string.Empty;

    public bool HomeIsWinner => ComputeWinner(home: true);
    public bool AwayIsWinner => ComputeWinner(home: false);
    public string HomeResultLabel => !HasResult ? string.Empty : HomeIsWinner ? "победа" : "проиграл";
    public string AwayResultLabel => !HasResult ? string.Empty : AwayIsWinner ? "победа" : "проиграл";

    private bool ComputeWinner(bool home)
    {
        if (!HasResult) return false;
        if (HomeGoals != AwayGoals)
            return home ? HomeGoals > AwayGoals : AwayGoals > HomeGoals;
        if (OutcomeType == OutcomeType.Shootout)
            return home ? ShootoutHome > ShootoutAway : ShootoutAway > ShootoutHome;
        return false;
    }

    private void RaiseScoreboardProps()
    {
        OnPropertyChanged(nameof(ScoreSuffix));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(HomeIsWinner));
        OnPropertyChanged(nameof(AwayIsWinner));
        OnPropertyChanged(nameof(HomeResultLabel));
        OnPropertyChanged(nameof(AwayResultLabel));
        OnPropertyChanged(nameof(HeaderDateText));
        OnPropertyChanged(nameof(CompetitionTitle));
    }

    public MatchEditViewModel(
        ITeamRepository teamRepository,
        IMatchRepository matchRepository,
        ITournamentRepository tournamentRepository,
        IStageTeamRepository stageTeamRepository,
        StatsService statsService,
        IMatchUpdatesNotifier matchUpdatesNotifier)
    {
        _teamRepository = teamRepository;
        _matchRepository = matchRepository;
        _tournamentRepository = tournamentRepository;
        _stageTeamRepository = stageTeamRepository;
        _statsService = statsService;
        _matchUpdatesNotifier = matchUpdatesNotifier;
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
        OnPropertyChanged(nameof(CompetitionTitle));
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
        await RefreshHeadToHeadAsync();
    }

    public async Task LoadMatchAsync()
    {
        if (MatchId == Guid.Empty) return;
        var match = await _matchRepository.GetByIdAsync(MatchId);
        if (match is null) return;

        SelectedMatchStatus = match.Status == MatchStatus.Cancelled ? MatchStatus.Scheduled : match.Status;
        StageId = match.StageId;
        SeriesId = match.SeriesId;
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

        await RefreshHeadToHeadAsync();
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
        var statusToSave = SelectedMatchStatus;

        int? finalHomeGoals = null;
        int? finalAwayGoals = null;
        OutcomeType? finalOutcomeType = null;
        Guid? winnerTeamId = null;
        Guid? loserTeamId = null;
        int? finalShootoutHome = null;
        int? finalShootoutAway = null;
        var periodScores = new List<PeriodScore>();

        if (statusToSave == MatchStatus.InProgress)
        {
            var hasDetailedPeriods =
                P1Home != 0 || P1Away != 0 ||
                P2Home != 0 || P2Away != 0 ||
                P3Home != 0 || P3Away != 0 ||
                (HasOvertime && (OTHome != 0 || OTAway != 0));

            if (hasDetailedPeriods)
            {
                periodScores.Add(new PeriodScore { PeriodNumber = 1, PeriodType = PeriodType.Regular, HomeGoals = P1Home, AwayGoals = P1Away });
                periodScores.Add(new PeriodScore { PeriodNumber = 2, PeriodType = PeriodType.Regular, HomeGoals = P2Home, AwayGoals = P2Away });
                periodScores.Add(new PeriodScore { PeriodNumber = 3, PeriodType = PeriodType.Regular, HomeGoals = P3Home, AwayGoals = P3Away });
                if (HasOvertime)
                    periodScores.Add(new PeriodScore { PeriodNumber = 4, PeriodType = PeriodType.Overtime, HomeGoals = OTHome, AwayGoals = OTAway });

                finalHomeGoals = P1Home + P2Home + P3Home + (HasOvertime ? OTHome : 0);
                finalAwayGoals = P1Away + P2Away + P3Away + (HasOvertime ? OTAway : 0);
            }
            else
            {
                finalHomeGoals = HomeGoals;
                finalAwayGoals = AwayGoals;
            }
        }
        else if (statusToSave == MatchStatus.Finished)
        {
            var hasDetailedPeriods =
                P1Home != 0 || P1Away != 0 ||
                P2Home != 0 || P2Away != 0 ||
                P3Home != 0 || P3Away != 0 ||
                (HasOvertime && (OTHome != 0 || OTAway != 0)) ||
                (OutcomeType == OutcomeType.Shootout && (ShootoutHome != 0 || ShootoutAway != 0));

            if (hasDetailedPeriods)
            {
                periodScores.Add(new PeriodScore { PeriodNumber = 1, PeriodType = PeriodType.Regular, HomeGoals = P1Home, AwayGoals = P1Away });
                periodScores.Add(new PeriodScore { PeriodNumber = 2, PeriodType = PeriodType.Regular, HomeGoals = P2Home, AwayGoals = P2Away });
                periodScores.Add(new PeriodScore { PeriodNumber = 3, PeriodType = PeriodType.Regular, HomeGoals = P3Home, AwayGoals = P3Away });
                if (HasOvertime)
                    periodScores.Add(new PeriodScore { PeriodNumber = 4, PeriodType = PeriodType.Overtime, HomeGoals = OTHome, AwayGoals = OTAway });

                if (OutcomeType == OutcomeType.Shootout)
                {
                    if (ShootoutHome == ShootoutAway)
                        return false;

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

                if (OutcomeType == OutcomeType.Shootout)
                {
                    if (totalHome != totalAway)
                        return false;

                    if (ShootoutHome > ShootoutAway)
                        totalHome += 1;
                    else
                        totalAway += 1;
                }
                else if (totalHome == totalAway)
                {
                    return false;
                }

                finalHomeGoals = totalHome;
                finalAwayGoals = totalAway;
                finalOutcomeType = OutcomeType;
                finalShootoutHome = OutcomeType == OutcomeType.Shootout ? ShootoutHome : null;
                finalShootoutAway = OutcomeType == OutcomeType.Shootout ? ShootoutAway : null;
            }
            else
            {
                if (HomeGoals == AwayGoals)
                    return false;
                if (OutcomeType == OutcomeType.Shootout && ShootoutHome == ShootoutAway)
                    return false;

                finalHomeGoals = HomeGoals;
                finalAwayGoals = AwayGoals;
                finalOutcomeType = OutcomeType;
                finalShootoutHome = OutcomeType == OutcomeType.Shootout ? ShootoutHome : null;
                finalShootoutAway = OutcomeType == OutcomeType.Shootout ? ShootoutAway : null;
            }

            winnerTeamId = finalHomeGoals > finalAwayGoals ? HomeTeam.Id : AwayTeam.Id;
            loserTeamId = winnerTeamId == HomeTeam.Id ? AwayTeam.Id : HomeTeam.Id;
        }

        var match = new Match
        {
            Id = id,
            TournamentId = TournamentId,
            StageId = StageId,
            SeriesId = SeriesId,
            DateTime = dateTime,
            HomeTeamId = HomeTeam.Id,
            AwayTeamId = AwayTeam.Id,
            HomeGoals = finalHomeGoals,
            AwayGoals = finalAwayGoals,
            OutcomeType = finalOutcomeType,
            WinnerTeamId = winnerTeamId,
            LoserTeamId = loserTeamId,
            ShootoutScoreHome = finalShootoutHome,
            ShootoutScoreAway = finalShootoutAway,
            Status = statusToSave,
            PeriodScores = periodScores
        };

        await _matchRepository.SaveAsync(match);
        _matchUpdatesNotifier.Publish(new MatchUpdatedMessage(
            TournamentId,
            match.Id,
            StageId,
            statusToSave));
        return true;
    }

    private async Task RefreshHeadToHeadAsync()
    {
        if (TournamentId == Guid.Empty || HomeTeam is null || AwayTeam is null || StageId is not { } stageId || stageId == Guid.Empty)
        {
            HeadToHead = new HeadToHeadStats();
            OnPropertyChanged(nameof(HasHeadToHead));
            return;
        }

        var matches = await _matchRepository.GetByTournamentAsync(TournamentId);
        var stageMatches = matches.Where(m => m.StageId == stageId).ToList();
        HeadToHead = _statsService.CalculateHeadToHead(stageMatches, HomeTeam.Id, AwayTeam.Id);
        OnPropertyChanged(nameof(HasHeadToHead));
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

