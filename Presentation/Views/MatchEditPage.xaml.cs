using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class MatchEditPage : ContentPage, IQueryAttributable
{
    private readonly MatchEditViewModel _viewModel;

    public MatchEditPage(MatchEditViewModel viewModel)
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
            await _viewModel.LoadTeamsAsync();
        }
    }

    private void OnOutcomeChanged(object? sender, EventArgs e)
    {
        if (sender is not Picker picker) return;

        _viewModel.OutcomeType = picker.SelectedIndex switch
        {
            1 => OutcomeType.Overtime,
            2 => OutcomeType.Shootout,
            _ => OutcomeType.Regulation
        };

        ShootoutPanel.IsVisible = _viewModel.OutcomeType == OutcomeType.Shootout;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var success = await _viewModel.SaveAsync();
        if (!success)
        {
            await DisplayAlert("Ошибка", "Проверьте команды и счёт.", "OK");
            return;
        }

        await Shell.Current.GoToAsync("..");
    }
}

