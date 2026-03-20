using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Resources;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class StageMatchesPage : ContentPage, IQueryAttributable
{
    private readonly StageDetailsViewModel _viewModel;
    private readonly ObservableCollection<MatchRow> _filteredMatches = new();
    private Guid _tournamentId;
    private Guid _stageId;
    private Guid? _seriesId;

    public StageMatchesPage(StageDetailsViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
        MatchesCollection.ItemsSource = _filteredMatches;

        PeriodFilterPicker.SelectedIndex = 0;
        StatusFilterPicker.SelectedIndex = 0;
        var today = DateTime.Today;
        FromDatePicker.Date = today.AddDays(-30);
        ToDatePicker.Date = today;

        _viewModel.StageMatches.CollectionChanged += OnStageMatchesCollectionChanged;
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
            await _viewModel.LoadAsync(_tournamentId, _stageId);
            ApplyFilters();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_tournamentId != Guid.Empty && _stageId != Guid.Empty)
        {
            await _viewModel.LoadAsync(_tournamentId, _stageId);
            ApplyFilters();
        }
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

        await _viewModel.DeleteMatchAsync(row.MatchId);
        ApplyFilters();
    }

    private void OnTeamSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        CustomRangePanel.IsVisible = PeriodFilterPicker.SelectedIndex == 5;
        ApplyFilters();
    }

    private void OnFilterChangedDate(object? sender, DateChangedEventArgs e)
    {
        ApplyFilters();
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
        ApplyFilters();
    }

    private void OnStageMatchesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<MatchRow> source = _viewModel.StageMatches;
        if (_seriesId.HasValue)
            source = source.Where(r => r.SeriesId == _seriesId.Value);

        var query = TeamSearchBar.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            source = source.Where(r =>
                r.HomeTeamName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                r.AwayTeamName.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        }

        var status = StatusFilterPicker.SelectedIndex switch
        {
            1 => MatchStatus.Scheduled,
            2 => MatchStatus.InProgress,
            3 => MatchStatus.Finished,
            4 => MatchStatus.Cancelled,
            _ => (MatchStatus?)null
        };
        if (status.HasValue)
            source = source.Where(r => r.Status == status.Value);

        var now = DateTime.Now;
        DateTime? from = null;
        DateTime? to = null;
        switch (PeriodFilterPicker.SelectedIndex)
        {
            case 1:
                var dayOffset = ((int)now.DayOfWeek + 6) % 7;
                from = now.Date.AddDays(-dayOffset);
                to = from.Value.AddDays(7).AddTicks(-1);
                break;
            case 2:
                from = new DateTime(now.Year, now.Month, 1);
                to = from.Value.AddMonths(1).AddTicks(-1);
                break;
            case 3:
                from = new DateTime(now.Year, 1, 1);
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

        if (from.HasValue)
            source = source.Where(r => r.DateTime.HasValue && r.DateTime.Value >= from.Value);
        if (to.HasValue)
            source = source.Where(r => r.DateTime.HasValue && r.DateTime.Value <= to.Value);

        var result = source
            .OrderByDescending(r => r.DateTime ?? DateTime.MinValue)
            .ToList();

        _filteredMatches.Clear();
        foreach (var row in result)
            _filteredMatches.Add(row);

        MatchesCounterLabel.Text = $"Найдено матчей: {_filteredMatches.Count}";
    }
}
