using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Resources;

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

    private async void OnTeamZoneAssignmentChanged(object? sender, EventArgs e)
    {
        if (sender is not Picker p || p.BindingContext is not TeamZoneAssignmentRow row)
            return;
        await _viewModel.SetStandingRowZoneFromPickerAsync(row.TeamId, p.SelectedIndex);
    }

    private async void OnAddZoneClicked(object? sender, EventArgs e) =>
        await _viewModel.AddColorZoneAsync();

    private async void OnZoneDeleteClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not StageColorZoneListItem zone)
            return;
        if (!await DisplayAlert(AppResources.Delete, AppResources.DeleteColorZoneConfirm, AppResources.Ok, AppResources.Cancel))
            return;
        await _viewModel.DeleteColorZoneAsync(zone.ZoneId);
    }

    private async void OnZoneChangeColorClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not StageColorZoneListItem zone)
            return;

        var options = new (string Label, string Hex)[]
        {
            ("Красный", "#EF4444"),
            ("Зелёный", "#22C55E"),
            ("Жёлтый", "#EAB308"),
            ("Синий", "#3B82F6"),
            ("Голубой", "#38BDF8"),
            ("Фиолетовый", "#A855F7"),
            ("Серый", "#6B7280")
        };

        var pick = await DisplayActionSheet(
            AppResources.ChangeColor,
            AppResources.Cancel,
            null,
            options.Select(o => o.Label).ToArray());

        if (string.IsNullOrEmpty(pick) || pick == AppResources.Cancel)
            return;

        var hex = options.FirstOrDefault(o => o.Label == pick).Hex;
        if (hex is not null)
            await _viewModel.UpdateZoneColorAsync(zone.ZoneId, hex);
    }
}

