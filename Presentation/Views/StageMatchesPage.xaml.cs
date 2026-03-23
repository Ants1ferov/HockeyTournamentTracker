using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Resources;
using HockeyTournamentTracker.Data;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class StageMatchesPage : ContentPage, IQueryAttributable
{
    private readonly StageDetailsViewModel _viewModel;
    private readonly IMatchRepository _matchRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ObservableCollection<MatchRow> _filteredMatches = new();
    private readonly Dictionary<Guid, string> _teamNameById = new();
    private Guid _tournamentId;
    private Guid _stageId;
    private Guid? _seriesId;
    private Tournament? _tournament;
    private int _currentOffset;
    private const int PageSize = 50;
    private bool _hasMore;
    private bool _isLoading;
    private CancellationTokenSource? _searchDebounceCts;

    public StageMatchesPage(
        StageDetailsViewModel viewModel,
        IMatchRepository matchRepository,
        ITeamRepository teamRepository,
        ITournamentRepository tournamentRepository)
    {
        _viewModel = viewModel;
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _tournamentRepository = tournamentRepository;
        BindingContext = _viewModel;
        InitializeComponent();
        MatchesCollection.ItemsSource = _filteredMatches;

        PeriodFilterPicker.SelectedIndex = 0;
        StatusFilterPicker.SelectedIndex = 0;
        var today = DateTime.Today;
        FromDatePicker.Date = today.AddDays(-30);
        ToDatePicker.Date = today;

    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("TournamentId", out var tVal) &&
            query.TryGetValue("StageId", out var sVal) &&
            tVal is string tStr && sVal is string sStr &&
            Guid.TryParse(tStr, out var tournamentId) &&
            Guid.TryParse(sStr, out var stageId))
        {
            _tournamentId = tournamentId;
            _stageId = stageId;
            if (query.TryGetValue("SeriesId", out var seriesVal) && seriesVal is string seriesStr && Guid.TryParse(seriesStr, out var seriesId))
                _seriesId = seriesId;
            else
                _seriesId = null;
            _tournament = await _tournamentRepository.GetByIdAsync(_tournamentId);
            await EnsureTeamNamesLoadedAsync();
            await ReloadFromRepositoryAsync();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_tournamentId != Guid.Empty && _stageId != Guid.Empty)
        {
            await EnsureTeamNamesLoadedAsync();
            await ReloadFromRepositoryAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = null;
    }

    private async void OnAddMatchClicked(object? sender, EventArgs e)
    {
        if (_stageId == Guid.Empty || _tournamentId == Guid.Empty)
            return;

        var route = $"{nameof(MatchEditPage)}?TournamentId={_tournamentId}&StageId={_stageId}";
        if (_seriesId.HasValue)
            route += $"&SeriesId={_seriesId.Value}";
        await Shell.Current.GoToAsync(route);
    }

    private async void OnEditMatchClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not MatchRow row || _tournamentId == Guid.Empty)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(MatchEditPage)}?TournamentId={_tournamentId}&MatchId={row.MatchId}");
    }

    private async void OnDeleteMatchClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not MatchRow row)
            return;

        var ok = await DisplayAlert(
            AppResources.Delete,
            "Удалить матч?",
            AppResources.Ok,
            AppResources.Cancel);
        if (!ok)
            return;

        await _matchRepository.DeleteAsync(row.MatchId);
        await ReloadFromRepositoryAsync();
    }

    private void OnTeamSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                if (token.IsCancellationRequested)
                    return;

                await MainThread.InvokeOnMainThreadAsync(ReloadFromRepositoryAsync);
            }
            catch (TaskCanceledException)
            {
                // noop
            }
        }, token);
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        CustomRangePanel.IsVisible = PeriodFilterPicker.SelectedIndex == 5;
        _ = ReloadFromRepositoryAsync();
    }

    private void OnFilterChangedDate(object? sender, DateChangedEventArgs e)
    {
        _ = ReloadFromRepositoryAsync();
    }

    private void OnResetFiltersClicked(object? sender, EventArgs e)
    {
        TeamSearchBar.Text = string.Empty;
        PeriodFilterPicker.SelectedIndex = 0;
        StatusFilterPicker.SelectedIndex = 0;
        CustomRangePanel.IsVisible = false;
        var today = DateTime.Today;
        FromDatePicker.Date = today.AddDays(-30);
        ToDatePicker.Date = today;
        _ = ReloadFromRepositoryAsync();
    }

    private async void OnRemainingItemsThresholdReached(object? sender, EventArgs e)
    {
        if (!MatchesCollection.IsVisible)
            return;
        if (_isLoading || !_hasMore)
            return;

        await LoadNextPageAsync();
    }

    private async void OnLoadMoreClicked(object? sender, EventArgs e)
    {
        await LoadNextPageAsync();
    }

    private async Task ReloadFromRepositoryAsync()
    {
        if (_tournamentId == Guid.Empty || _stageId == Guid.Empty)
            return;

        if (_isLoading)
            return;

        _currentOffset = 0;
        _hasMore = false;
        _filteredMatches.Clear();
        await LoadNextPageAsync();
    }

    private async Task LoadNextPageAsync()
    {
        if (_tournamentId == Guid.Empty || _stageId == Guid.Empty || _isLoading)
            return;

        _isLoading = true;
        SetLoadingUi(true);
        try
        {
            var query = BuildQuery(_currentOffset, PageSize);
            var page = await _matchRepository.GetByStageAsync(query);

            var rows = page.Items.Select(MapToRow).ToList();
            foreach (var row in rows)
                _filteredMatches.Add(row);

            _currentOffset += rows.Count;
            _hasMore = page.HasMore;
            MatchesCounterLabel.Text = $"Показано матчей: {_filteredMatches.Count}";
            LoadMoreButton.IsVisible = _hasMore;
        }
        finally
        {
            _isLoading = false;
            SetLoadingUi(false);
        }
    }

    private StageMatchQuery BuildQuery(int offset, int limit)
    {
        var status = StatusFilterPicker.SelectedIndex switch
        {
            1 => MatchStatus.Scheduled,
            2 => MatchStatus.InProgress,
            3 => MatchStatus.Finished,
            4 => MatchStatus.Cancelled,
            _ => (MatchStatus?)null
        };

        var now = DateTime.Now;
        var baselineDate = _tournament?.StartDate?.Date;
        var baseline = baselineDate.HasValue && baselineDate.Value <= now.Date
            ? baselineDate.Value
            : now.Date;
        DateTime? from = null;
        DateTime? to = null;
        switch (PeriodFilterPicker.SelectedIndex)
        {
            case 1:
                var daysSinceBaseline = (now.Date - baseline).Days;
                var weekStart = baseline.AddDays((daysSinceBaseline / 7) * 7);
                from = weekStart;
                to = from.Value.AddDays(7).AddTicks(-1);
                break;
            case 2:
                from = new DateTime(baseline.Year, baseline.Month, 1);
                while (from.Value.AddMonths(1) <= now)
                    from = from.Value.AddMonths(1);
                to = from.Value.AddMonths(1).AddTicks(-1);
                break;
            case 3:
                from = new DateTime(baseline.Year, 1, 1);
                while (from.Value.AddYears(1) <= now)
                    from = from.Value.AddYears(1);
                to = from.Value.AddYears(1).AddTicks(-1);
                break;
            case 4:
                from = now.Date.AddDays(-30);
                to = now;
                break;
            case 5:
                var fromDate = FromDatePicker.Date ?? DateTime.Today;
                var toDate = ToDatePicker.Date ?? fromDate;
                from = fromDate.Date;
                to = toDate.Date.AddDays(1).AddTicks(-1);
                break;
        }

        if (from.HasValue && to.HasValue && from > to)
        {
            var tmp = from.Value;
            from = to.Value;
            to = tmp;
        }

        return new StageMatchQuery
        {
            TournamentId = _tournamentId,
            StageId = _stageId,
            SeriesId = _seriesId,
            TeamSearch = TeamSearchBar.Text?.Trim(),
            Status = status,
            DateFrom = from,
            DateTo = to,
            Offset = offset,
            Limit = limit,
            SortDescending = true
        };
    }

    private MatchRow MapToRow(Match match)
    {
        _teamNameById.TryGetValue(match.HomeTeamId, out var homeName);
        _teamNameById.TryGetValue(match.AwayTeamId, out var awayName);
        return new MatchRow
        {
            MatchId = match.Id,
            SeriesId = match.SeriesId,
            DateTime = match.DateTime,
            HomeTeamName = homeName ?? string.Empty,
            AwayTeamName = awayName ?? string.Empty,
            DisplayScore = BuildScoreText(match),
            IsLive = match.Status == MatchStatus.InProgress,
            Status = match.Status
        };
    }

    private async Task EnsureTeamNamesLoadedAsync()
    {
        if (_tournamentId == Guid.Empty)
            return;
        if (_teamNameById.Count > 0)
            return;

        var teams = await _teamRepository.GetByTournamentAsync(_tournamentId);
        _teamNameById.Clear();
        foreach (var t in teams)
            _teamNameById[t.Id] = t.Name;
    }

    private void SetLoadingUi(bool loading)
    {
        LoadingIndicator.IsVisible = loading;
        LoadingIndicator.IsRunning = loading;
        LoadMoreButton.IsEnabled = !loading;
    }

    private static string BuildScoreText(Match match)
    {
        if (match.HomeGoals is null || match.AwayGoals is null)
            return "— : —";

        var finalHomeGoals = match.HomeGoals.Value;
        var finalAwayGoals = match.AwayGoals.Value;
        if (match.Status == MatchStatus.Finished && match.OutcomeType is not null)
        {
            var effectiveScore = MatchOutcomeResolver.GetEffectiveFinalScore(match);
            finalHomeGoals = effectiveScore?.HomeGoals ?? finalHomeGoals;
            finalAwayGoals = effectiveScore?.AwayGoals ?? finalAwayGoals;
        }

        var baseScore = $"{finalHomeGoals}:{finalAwayGoals}";
        var periodPart = "";
        if (match.PeriodScores is { Count: > 0 } periods)
            periodPart = " (" + string.Join(", ", periods.Select(p => $"{p.HomeGoals}:{p.AwayGoals}")) + ")";

        var hasOvertimePeriod = match.PeriodScores?.Any(p => p.PeriodType == PeriodType.Overtime) == true;
        var hasShootoutPeriod = match.PeriodScores?.Any(p => p.PeriodType == PeriodType.Shootout) == true;
        var outcomeSuffix = match.OutcomeType switch
        {
            OutcomeType.Overtime => hasOvertimePeriod ? "" : " ОТ",
            OutcomeType.Shootout => hasShootoutPeriod ? "" : (match.ShootoutScoreHome is { } sh && match.ShootoutScoreAway is { } sa
                ? $" Б ({sh}:{sa})"
                : " Б"),
            _ => ""
        };

        return $"{baseScore}{outcomeSuffix}{periodPart}";
    }
}
