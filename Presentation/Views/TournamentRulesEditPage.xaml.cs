using HockeyTournamentTracker.Presentation.ViewModels;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class TournamentRulesEditPage : ContentPage, IQueryAttributable
{
    private readonly TournamentRulesEditViewModel _viewModel;

    public TournamentRulesEditPage(TournamentRulesEditViewModel viewModel)
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

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (await _viewModel.SaveAsync())
            await Shell.Current.GoToAsync("..");
    }
}
