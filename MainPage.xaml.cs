using HockeyTournamentTracker.Presentation;
using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Presentation.Views;
using HockeyTournamentTracker.Resources;

namespace HockeyTournamentTracker;

public partial class MainPage : ContentPage
{
    private readonly TournamentsListViewModel _viewModel;
    private readonly IAppThemeSettings _themeSettings;

    public MainPage(TournamentsListViewModel viewModel, IAppThemeSettings themeSettings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _themeSettings = themeSettings;
        BindingContext = _viewModel;
        ThemePicker.SelectedIndex = _themeSettings.GetSelectedIndex();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (ThemePicker.SelectedIndex < 0)
            return;
        _themeSettings.ApplyByIndex(ThemePicker.SelectedIndex);
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

    private async void OnTournamentTapped(object? sender, TappedEventArgs e)
    {
        var tournament = (sender as BindableObject)?.BindingContext as Domain.Tournament
            ?? (sender as TapGestureRecognizer)?.Parent?.BindingContext as Domain.Tournament;
        if (tournament is null) return;
        await Shell.Current.GoToAsync($"{nameof(TournamentDetailsPage)}?TournamentId={tournament.Id}");
    }

    private async void OnDeleteTournamentInvoked(object? sender, EventArgs e)
    {
        var tournament = (sender as SwipeItem)?.Parent?.Parent is BindableObject bo
            ? bo.BindingContext as Domain.Tournament
            : null;
        if (tournament is null) return;
        var confirm = await DisplayAlert(AppResources.Delete, AppResources.DeleteTournamentConfirm, AppResources.Delete, AppResources.Cancel);
        if (!confirm) return;
        await _viewModel.DeleteTournamentAsync(tournament);
    }
}
