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
        ThemePicker.Items.Clear();
        ThemePicker.Items.Add(AppResources.ThemeSystem);
        ThemePicker.Items.Add(AppResources.ThemeLight);
        ThemePicker.Items.Add(AppResources.ThemeDark);
        ThemePicker.SelectedIndex = _themeSettings.GetSelectedIndex();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (ThemePicker.SelectedIndex < 0) return;
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

    private async void OnAddLeagueClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LeagueEditPage));
    }

    private async void OnTournamentTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not TournamentCardVm card) return;
        await Shell.Current.GoToAsync($"{nameof(TournamentDetailsPage)}?TournamentId={card.Id}");
    }

    private void OnLeagueTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not LeagueCardVm league) return;
        league.IsExpanded = !league.IsExpanded;
    }

    private async void OnEditLeagueInvoked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not LeagueCardVm league) return;
        await Shell.Current.GoToAsync($"{nameof(LeagueEditPage)}?LeagueId={league.Id}");
    }

    private async void OnDeleteTournamentInvoked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not TournamentCardVm card) return;
        var confirm = await DisplayAlert(AppResources.Delete, AppResources.DeleteTournamentConfirm, AppResources.Delete, AppResources.Cancel);
        if (!confirm) return;
        await _viewModel.DeleteTournamentAsync(card.Id);
    }

    private async void OnDeleteLeagueInvoked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not LeagueCardVm league) return;
        var confirm = await DisplayAlert("Удалить лигу", "Лига будет удалена. Турниры останутся.", "Удалить", AppResources.Cancel);
        if (!confirm) return;
        await _viewModel.DeleteLeagueAsync(league);
    }
}
