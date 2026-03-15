using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class TournamentDetailsPage : ContentPage, IQueryAttributable
{
    private readonly TournamentDetailsViewModel _viewModel;

    public TournamentDetailsPage(TournamentDetailsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
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

    private async void OnGroupsClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Tournament is null) return;
        await Shell.Current.GoToAsync($"{nameof(GroupsListPage)}?TournamentId={_viewModel.Tournament.Id}");
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

