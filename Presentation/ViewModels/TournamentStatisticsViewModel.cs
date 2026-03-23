using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.Services;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class TournamentStatisticsViewModel : INotifyPropertyChanged, IMatchUpdatesListener
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IStageRepository _stageRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IStageTeamRepository _stageTeamRepository;
    private readonly IMatchUpdatesNotifier _matchUpdatesNotifier;

    private Guid _tournamentId;
    private Tournament? _tournament;
    private Stage? _selectedSwissStage;
    private Team? _selectedDashboardTeam;

    public Guid TournamentId
    {
        get => _tournamentId;
        set => SetField(ref _tournamentId, value);
    }

    public Tournament? Tournament
    {
        get => _tournament;
        private set
        {
            if (SetField(ref _tournament, value))
                OnPropertyChanged(nameof(PageTitle));
        }
    }

    public string PageTitle => Tournament?.Name ?? string.Empty;

    public ObservableCollection<Stage> SwissStages { get; } = new();
    public ObservableCollection<Team> DashboardTeams { get; } = new();

    public Stage? SelectedSwissStage
    {
        get => _selectedSwissStage;
        set
        {
            if (SetField(ref _selectedSwissStage, value))
                _ = ReloadDashboardAsync();
        }
    }

    public Team? SelectedDashboardTeam
    {
        get => _selectedDashboardTeam;
        set
        {
            if (SetField(ref _selectedDashboardTeam, value))
                _ = ReloadDashboardAsync();
        }
    }

    public ObservableCollection<TournamentStatisticsCalculator.PeriodTeamRow> Period1Rows { get; } = new();
    public ObservableCollection<TournamentStatisticsCalculator.PeriodTeamRow> Period2Rows { get; } = new();
    public ObservableCollection<TournamentStatisticsCalculator.PeriodTeamRow> Period3Rows { get; } = new();
    public ObservableCollection<TournamentStatisticsCalculator.GoalsTeamRow> GoalsForRows { get; } = new();
    public ObservableCollection<TournamentStatisticsCalculator.GoalsTeamRow> GoalsAgainstRows { get; } = new();
    public ObservableCollection<TournamentStatisticsCalculator.MonthlyPlaceRow> MonthlyRows { get; } = new();

    public TournamentStatisticsViewModel(
        ITournamentRepository tournamentRepository,
        ITeamRepository teamRepository,
        IStageRepository stageRepository,
        IMatchRepository matchRepository,
        IStageTeamRepository stageTeamRepository,
        IMatchUpdatesNotifier matchUpdatesNotifier)
    {
        _tournamentRepository = tournamentRepository;
        _teamRepository = teamRepository;
        _stageRepository = stageRepository;
        _matchRepository = matchRepository;
        _stageTeamRepository = stageTeamRepository;
        _matchUpdatesNotifier = matchUpdatesNotifier;
        _matchUpdatesNotifier.Subscribe(this);
    }

    public async Task LoadAsync(Guid tournamentId)
    {
        TournamentId = tournamentId;
        Tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (Tournament is null)
            return;

        var teams = await _teamRepository.GetByTournamentAsync(tournamentId);
        var allMatches = await _matchRepository.GetByTournamentAsync(tournamentId);
        var finished = allMatches.Where(m => m.Status == MatchStatus.Finished && m.OutcomeType.HasValue).ToList();

        var stages = await _stageRepository.GetByTournamentAsync(tournamentId);
        var swiss = stages.Where(s => s.StageType == StageType.Swiss).ToList();

        var p1 = TournamentStatisticsCalculator.BuildPeriodTable(teams, finished, 1);
        var p2 = TournamentStatisticsCalculator.BuildPeriodTable(teams, finished, 2);
        var p3 = TournamentStatisticsCalculator.BuildPeriodTable(teams, finished, 3);
        var gf = TournamentStatisticsCalculator.BuildGoalsForTable(teams, finished);
        var ga = TournamentStatisticsCalculator.BuildGoalsAgainstTable(teams, finished);

        await RunOnMainThreadAsync(() =>
        {
            SwissStages.Clear();
            foreach (var s in swiss)
                SwissStages.Add(s);

            if (swiss.Count > 0 && (_selectedSwissStage is null || swiss.All(x => x.Id != _selectedSwissStage.Id)))
            {
                _selectedSwissStage = swiss[0];
                OnPropertyChanged(nameof(SelectedSwissStage));
            }

            Period1Rows.Clear();
            foreach (var r in p1) Period1Rows.Add(r);
            Period2Rows.Clear();
            foreach (var r in p2) Period2Rows.Add(r);
            Period3Rows.Clear();
            foreach (var r in p3) Period3Rows.Add(r);
            GoalsForRows.Clear();
            foreach (var r in gf) GoalsForRows.Add(r);
            GoalsAgainstRows.Clear();
            foreach (var r in ga) GoalsAgainstRows.Add(r);
        });

        await ReloadDashboardAsync();
    }

    private async Task ReloadDashboardAsync()
    {
        if (Tournament is null || SelectedSwissStage is null)
        {
            await RunOnMainThreadAsync(() =>
            {
                DashboardTeams.Clear();
                MonthlyRows.Clear();
            });
            return;
        }

        var allTeams = await _teamRepository.GetByTournamentAsync(Tournament.Id);
        var stageId = SelectedSwissStage.Id;
        var stageTeamIds = (await _stageTeamRepository.GetTeamIdsByStageAsync(stageId)).ToHashSet();
        var matches = (await _matchRepository.GetByTournamentAsync(Tournament.Id))
            .Where(m => m.StageId == stageId)
            .ToList();

        if (stageTeamIds.Count == 0 && matches.Count > 0)
        {
            stageTeamIds = matches
                .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
                .Distinct()
                .ToHashSet();
        }

        var teamsInStage = allTeams
            .Where(t => stageTeamIds.Contains(t.Id))
            .OrderBy(t => t.Name, StringComparer.Create(CultureInfo.CurrentCulture, ignoreCase: true))
            .ToList();

        var effectiveTeam = teamsInStage.FirstOrDefault(t => t.Id == _selectedDashboardTeam?.Id)
                           ?? teamsInStage.FirstOrDefault();

        await RunOnMainThreadAsync(() =>
        {
            DashboardTeams.Clear();
            foreach (var t in teamsInStage)
                DashboardTeams.Add(t);

            if (effectiveTeam is not null && (_selectedDashboardTeam is null || _selectedDashboardTeam.Id != effectiveTeam.Id))
            {
                _selectedDashboardTeam = effectiveTeam;
                OnPropertyChanged(nameof(SelectedDashboardTeam));
            }
            else if (effectiveTeam is null && _selectedDashboardTeam is not null)
            {
                _selectedDashboardTeam = null;
                OnPropertyChanged(nameof(SelectedDashboardTeam));
            }
        });

        if (effectiveTeam is null || Tournament is null)
        {
            await RunOnMainThreadAsync(() => MonthlyRows.Clear());
            return;
        }

        var monthly = TournamentStatisticsCalculator.BuildMonthlyStandingsPlaces(
            Tournament,
            teamsInStage,
            matches,
            effectiveTeam.Id);

        await RunOnMainThreadAsync(() =>
        {
            MonthlyRows.Clear();
            foreach (var m in monthly)
                MonthlyRows.Add(m);
        });
    }

    private static Task RunOnMainThreadAsync(Action action)
    {
        var tcs = new TaskCompletionSource();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
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

    public void OnMatchUpdated(MatchUpdatedMessage message)
    {
        if (TournamentId == Guid.Empty || message.TournamentId != TournamentId)
            return;

        _ = LoadAsync(TournamentId);
    }
}
