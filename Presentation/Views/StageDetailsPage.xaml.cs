using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class StageDetailsPage : ContentPage, IQueryAttributable
{
    private readonly StageDetailsViewModel _viewModel;

    public StageDetailsPage(StageDetailsViewModel viewModel)
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
            await _viewModel.LoadAsync(tournamentId, stageId);
        }
    }

    private async void OnManageRosterClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Stage is null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(StageRosterPage)}?TournamentId={_viewModel.Stage.TournamentId}&StageId={_viewModel.Stage.Id}");
    }

    private async void OnEditStageClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Stage is null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(StageEditPage)}?TournamentId={_viewModel.Stage.TournamentId}&StageId={_viewModel.Stage.Id}");
    }

    private async void OnOpenMatchesClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Stage is null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(StageMatchesPage)}?TournamentId={_viewModel.Stage.TournamentId}&StageId={_viewModel.Stage.Id}");
    }

    private async void OnOpenPlayoffClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Stage is null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(PlayoffBracketPage)}?TournamentId={_viewModel.Stage.TournamentId}&StageId={_viewModel.Stage.Id}");
    }
}

