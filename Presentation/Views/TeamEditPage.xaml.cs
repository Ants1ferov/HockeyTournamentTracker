using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Resources;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class TeamEditPage : ContentPage, IQueryAttributable
{
    private readonly TeamEditViewModel _viewModel;

    public TeamEditPage(TeamEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("TournamentId", out var tVal) && tVal is string tStr && Guid.TryParse(tStr, out var tournamentId))
        {
            _viewModel.TournamentId = tournamentId;
        }

        if (query.TryGetValue("TeamId", out var idVal) && idVal is string idStr && Guid.TryParse(idStr, out var teamId))
        {
            _viewModel.TeamId = teamId;
            Title = AppResources.EditTeam;
        }
        else
        {
            Title = AppResources.NewTeam;
        }
        await _viewModel.LoadAsync();
    }

    private async void OnPickIconClicked(object? sender, EventArgs e)
    {
        await _viewModel.PickIconAsync();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var success = await _viewModel.SaveAsync();
        if (success)
            await Shell.Current.GoToAsync("..");
    }
}
