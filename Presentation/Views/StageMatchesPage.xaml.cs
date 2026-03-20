using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Resources;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class StageMatchesPage : ContentPage, IQueryAttributable
{
    private readonly StageDetailsViewModel _viewModel;
    private Guid _tournamentId;
    private Guid _stageId;

    public StageMatchesPage(StageDetailsViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
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
            await _viewModel.LoadAsync(_tournamentId, _stageId);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_tournamentId != Guid.Empty && _stageId != Guid.Empty)
            await _viewModel.LoadAsync(_tournamentId, _stageId);
    }

    private async void OnAddMatchClicked(object? sender, EventArgs e)
    {
        if (_stageId == Guid.Empty || _tournamentId == Guid.Empty)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(MatchEditPage)}?TournamentId={_tournamentId}&StageId={_stageId}");
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
    }

    private async void OnStartMatchClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not MatchRow row)
            return;

        await _viewModel.SetMatchStatusAsync(row.MatchId, MatchStatus.InProgress);
    }

    private async void OnFinishMatchClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not MatchRow row || _tournamentId == Guid.Empty)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(MatchEditPage)}?TournamentId={_tournamentId}&MatchId={row.MatchId}");
    }
}
