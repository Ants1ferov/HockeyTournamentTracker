using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Resources;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class StageDetailsPage : ContentPage, IQueryAttributable
{
    private readonly StageDetailsViewModel _viewModel;
    private readonly HashSet<Guid> _selectedAddTeamIds = new();

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

    private async void OnAddGroupClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync(
            AppResources.Groups,
            AppResources.GroupName,
            initialValue: "");
        if (string.IsNullOrWhiteSpace(name))
            return;

        await _viewModel.AddGroupAsync(name);
    }

    private async void OnRenameGroupClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not GroupInfo group)
            return;

        var newName = await DisplayPromptAsync(
            AppResources.Groups,
            AppResources.GroupName,
            initialValue: group.Name);
        if (string.IsNullOrWhiteSpace(newName))
            return;

        await _viewModel.RenameGroupAsync(group.Id, newName);
    }

    private async void OnDeleteGroupClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not GroupInfo group)
            return;

        var ok = await DisplayAlert(
            AppResources.Delete,
            "Удалить группу?",
            AppResources.Ok,
            AppResources.Cancel);
        if (!ok)
            return;

        await _viewModel.DeleteGroupAsync(group.Id);
    }

    private async void OnAddAllTeamsClicked(object? sender, EventArgs e)
    {
        await _viewModel.AddAllTeamsToStageAsync();
        _selectedAddTeamIds.Clear();
    }

    private async void OnAddSelectedTeamsClicked(object? sender, EventArgs e)
    {
        if (_selectedAddTeamIds.Count == 0)
            return;

        await _viewModel.AddSelectedTeamsToStageAsync(_selectedAddTeamIds.ToList());
        _selectedAddTeamIds.Clear();
    }

    private void OnAvailableTeamTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not TeamForAddUi teamUi)
            return;

        var teamId = teamUi.TeamId;
        if (_selectedAddTeamIds.Contains(teamId))
        {
            _selectedAddTeamIds.Remove(teamId);
            teamUi.IsSelectedForAdd = false;
        }
        else
        {
            _selectedAddTeamIds.Add(teamId);
            teamUi.IsSelectedForAdd = true;
        }
    }

    private void OnAvailableTeamsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedAddTeamIds.Clear();
        foreach (var item in e.CurrentSelection)
        {
            if (item is Team t)
                _selectedAddTeamIds.Add(t.Id);
        }
    }

    private async void OnEditStageClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Stage is null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(StageEditPage)}?TournamentId={_viewModel.Stage.TournamentId}&StageId={_viewModel.Stage.Id}");
    }

    private async void OnAddMatchClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Stage is null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(MatchEditPage)}?TournamentId={_viewModel.Stage.TournamentId}&StageId={_viewModel.Stage.Id}");
    }

    private async void OnGroupColumnHeaderTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel.MoveSelectedCount == 0)
            return;

        if ((sender as BindableObject)?.BindingContext is not StageGroupColumn col)
            return;

        await _viewModel.MoveSelectedTeamsToGroupAsync(col.GroupId);
    }

    private void OnStageTeamTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not StageTeamForUi chip)
            return;

        _viewModel.ToggleMoveTeamSelection(chip.TeamId);
    }

    private async void OnStartMatchClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not MatchRow row)
            return;

        await _viewModel.SetMatchStatusAsync(row.MatchId, MatchStatus.InProgress);
    }

    private async void OnFinishMatchClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not MatchRow row || _viewModel.Tournament is null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(MatchEditPage)}?TournamentId={_viewModel.Tournament.Id}&MatchId={row.MatchId}");
    }
}

