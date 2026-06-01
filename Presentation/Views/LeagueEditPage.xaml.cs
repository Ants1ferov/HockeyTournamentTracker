using HockeyTournamentTracker.Presentation.ViewModels;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class LeagueEditPage : ContentPage
{
    private readonly LeagueEditViewModel _viewModel;

    public LeagueEditPage(LeagueEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadTournamentsAsync();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var success = await _viewModel.SaveAsync();
        if (!success)
        {
            await DisplayAlert("Ошибка", "Введите название лиги.", "OK");
            return;
        }
        await Shell.Current.GoToAsync("..");
    }
}
