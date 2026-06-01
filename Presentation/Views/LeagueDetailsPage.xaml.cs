using HockeyTournamentTracker.Presentation.ViewModels;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class LeagueDetailsPage : ContentPage
{
    private readonly LeagueDetailsViewModel _viewModel;

    public LeagueDetailsPage(LeagueDetailsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    private async void OnEditLeagueClicked(object? sender, EventArgs e)
    {
        if (_viewModel.League is null) return;
        await Shell.Current.GoToAsync($"{nameof(LeagueEditPage)}?LeagueId={_viewModel.League.Id}");
    }

    private async void OnAddTournamentClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(TournamentEditPage));
    }

    private async void OnTournamentTapped(object? sender, TappedEventArgs e)
    {
        var tournament = (sender as BindableObject)?.BindingContext as Domain.Tournament;
        if (tournament is null) return;
        await Shell.Current.GoToAsync($"{nameof(TournamentDetailsPage)}?TournamentId={tournament.Id}");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Перезагружаем при возврате (например, после редактирования)
        if (Guid.TryParse(_viewModel.LeagueIdStr, out var id))
            await _viewModel.LoadAsync(id);
    }
}
