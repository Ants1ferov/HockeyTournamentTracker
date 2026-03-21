using System.Linq;
using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Resources;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class TournamentDetailsPage : ContentPage, IQueryAttributable
{
    private readonly TournamentDetailsViewModel _viewModel;

    public TournamentDetailsPage(TournamentDetailsViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("TournamentId", out var value) && value is string idString &&
            Guid.TryParse(idString, out var id))
        {
            await _viewModel.LoadAsync(id);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Tournament is { } t)
            await _viewModel.LoadAsync(t.Id);
    }

    private async void OnTeamsClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Tournament is null) return;
        await Shell.Current.GoToAsync($"{nameof(TeamsListPage)}?TournamentId={_viewModel.Tournament.Id}");
    }

    private async void OnStartMatchClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not MatchRow row || _viewModel.Tournament is null)
            return;
        await _viewModel.SetMatchStatusAsync(row.MatchId, MatchStatus.InProgress);
    }

    private async void OnFinishMatchClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not MatchRow row || _viewModel.Tournament is null)
            return;
        await Shell.Current.GoToAsync($"{nameof(MatchEditPage)}?TournamentId={_viewModel.Tournament.Id}&MatchId={row.MatchId}");
    }

    private void OnTabHomeClicked(object? sender, EventArgs e) => _viewModel.SelectedTabIndex = 0;
    private void OnTabStagesClicked(object? sender, EventArgs e) => _viewModel.SelectedTabIndex = 1;
    private void OnTabParticipantsClicked(object? sender, EventArgs e) => _viewModel.SelectedTabIndex = 2;

    private async void OnAddStageClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Tournament is null) return;
        await Shell.Current.GoToAsync($"{nameof(StageEditPage)}?TournamentId={_viewModel.Tournament.Id}");
    }

    private async void OnStageTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not Stage stage || _viewModel.Tournament is null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(StageDetailsPage)}?TournamentId={_viewModel.Tournament.Id}&StageId={stage.Id}");
    }

    private void OnStageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _viewModel.SelectedStage = e.CurrentSelection?.FirstOrDefault() as Stage;
    }

    private async void OnDeleteStageInvoked(object? sender, EventArgs e)
    {
        var stage = (sender as SwipeItem)?.Parent?.Parent is BindableObject bo
            ? bo.BindingContext as Stage
            : null;
        if (stage is null || _viewModel.Tournament is null)
            return;
        if (!await DisplayAlert(AppResources.Delete, AppResources.DeleteStageConfirm, AppResources.Ok, AppResources.Cancel))
            return;
        await _viewModel.DeleteStageAsync(stage.Id);
    }

    private async void OnAddMatchInStageClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Tournament is null || _viewModel.SelectedStage is null) return;
        await Shell.Current.GoToAsync($"{nameof(MatchEditPage)}?TournamentId={_viewModel.Tournament.Id}&StageId={_viewModel.SelectedStage.Id}");
    }

    private async void OnAddParticipantClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Tournament is null) return;
        await Shell.Current.GoToAsync($"{nameof(TeamEditPage)}?TournamentId={_viewModel.Tournament.Id}");
    }

    private async void OnParticipantTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not Team team || _viewModel.Tournament is null)
            return;
        await Shell.Current.GoToAsync($"{nameof(TeamEditPage)}?TournamentId={_viewModel.Tournament.Id}&TeamId={team.Id}");
    }

    private async void OnDeleteParticipantInvoked(object? sender, EventArgs e)
    {
        var team = (sender as SwipeItem)?.Parent?.Parent is BindableObject bo
            ? bo.BindingContext as Team
            : null;
        if (team is null || _viewModel.Tournament is null)
            return;
        if (!await DisplayAlert(AppResources.Delete, AppResources.DeleteTeamConfirm, AppResources.Ok, AppResources.Cancel))
            return;
        await _viewModel.DeleteParticipantAsync(team.Id);
    }

    private async void OnAddMatchClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Tournament is null)
            return;

        await Shell.Current.GoToAsync($"{nameof(MatchEditPage)}?TournamentId={_viewModel.Tournament.Id}");
    }

    private async void OnEditRulesClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Tournament is null)
            return;
        await Shell.Current.GoToAsync($"{nameof(TournamentRulesEditPage)}?TournamentId={_viewModel.Tournament.Id}");
    }
}

