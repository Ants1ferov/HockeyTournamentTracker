using HockeyTournamentTracker.Presentation.ViewModels;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class TournamentEditPage : ContentPage
{
    private readonly TournamentEditViewModel _viewModel;

    public TournamentEditPage(TournamentEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var success = await _viewModel.SaveAsync();
        if (!success)
        {
            await DisplayAlert("Ошибка", "Введите название турнира.", "OK");
            return;
        }

        await Shell.Current.GoToAsync("..");
    }
}

