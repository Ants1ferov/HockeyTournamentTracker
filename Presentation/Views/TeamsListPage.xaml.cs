using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Resources;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class TeamsListPage : ContentPage, IQueryAttributable
{
    private readonly TeamsListViewModel _viewModel;

    public TeamsListPage(TeamsListViewModel viewModel)
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
            _viewModel.TournamentId = id;
            await _viewModel.LoadAsync();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.TournamentId != Guid.Empty)
            await _viewModel.LoadAsync();
    }

    private async void OnAddTeamClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"{nameof(TeamEditPage)}?TournamentId={_viewModel.TournamentId}");
    }

    private async void OnTeamTapped(object? sender, TappedEventArgs e)
    {
        var team = (sender as BindableObject)?.BindingContext as Team
            ?? (sender as TapGestureRecognizer)?.Parent?.BindingContext as Team;
        if (team is null) return;
        await Shell.Current.GoToAsync($"{nameof(TeamEditPage)}?TournamentId={_viewModel.TournamentId}&TeamId={team.Id}");
    }

    private async void OnDeleteTeamInvoked(object? sender, EventArgs e)
    {
        // Контекст команды — в DataTemplate, родитель SwipeItem это SwipeView с BindingContext = Team
        var team = (sender as SwipeItem)?.Parent?.Parent is BindableObject bo
            ? bo.BindingContext as Team
            : null;
        if (team is null) return;
        var confirm = await DisplayAlert(AppResources.Delete, AppResources.DeleteTeamConfirm, AppResources.Delete, AppResources.Cancel);
        if (!confirm) return;
        await _viewModel.DeleteTeamAsync(team);
    }
}
