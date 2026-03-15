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

    private async void OnAddMatchClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Tournament is null)
            return;

        await Shell.Current.GoToAsync($"{nameof(MatchEditPage)}?TournamentId={_viewModel.Tournament.Id}");
    }
}

