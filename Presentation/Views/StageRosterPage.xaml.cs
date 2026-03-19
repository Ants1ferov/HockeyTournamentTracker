using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Resources;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class StageRosterPage : ContentPage, IQueryAttributable
{
    private readonly StageDetailsViewModel _viewModel;
    private readonly HashSet<Guid> _selectedAddTeamIds = new();

    public StageRosterPage(StageDetailsViewModel viewModel)
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

    private void OnStageTeamTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not StageTeamForUi chip)
            return;

        _viewModel.ToggleMoveTeamSelection(chip.TeamId);
    }
}
