using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class StageDetailsPage : ContentPage, IQueryAttributable
{
    private readonly StageDetailsViewModel _viewModel;
    private bool _isLoading;

    public StageDetailsPage(StageDetailsViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (_isLoading)
            return;

        if (query.TryGetValue("TournamentId", out var tVal) &&
            query.TryGetValue("StageId", out var sVal) &&
            tVal is string tStr && sVal is string sStr &&
            Guid.TryParse(tStr, out var tournamentId) &&
            Guid.TryParse(sStr, out var stageId))
        {
            _isLoading = true;
            try
            {
                await _viewModel.LoadAsync(tournamentId, stageId);
            }
            catch
            {
                // Keep page alive if loading fails unexpectedly.
            }
            finally
            {
                _isLoading = false;
            }
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

