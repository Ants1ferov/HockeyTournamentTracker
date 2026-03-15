using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Presentation.Views;

namespace HockeyTournamentTracker;

public partial class MainPage : ContentPage
{
    private readonly TournamentsListViewModel _viewModel;

    public MainPage(TournamentsListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnAddTournamentClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(TournamentEditPage));
    }

    private async void OnTournamentSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Domain.Tournament tournament)
            return;

        ((CollectionView)sender!).SelectedItem = null;

        await Shell.Current.GoToAsync($"{nameof(TournamentDetailsPage)}?TournamentId={tournament.Id}");
    }
}
