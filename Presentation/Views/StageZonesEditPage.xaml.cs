using HockeyTournamentTracker.Presentation.ViewModels;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class StageZonesEditPage : ContentPage, IQueryAttributable
{
    private readonly StageZonesEditViewModel _viewModel;

    public StageZonesEditPage(StageZonesEditViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("TournamentId", out var tVal) &&
            query.TryGetValue("StageId", out var sVal) &&
            tVal is string tStr && sVal is string sStr &&
            Guid.TryParse(tStr, out var tournamentId) &&
            Guid.TryParse(sStr, out var stageId))
        {
            await _viewModel.LoadAsync(tournamentId, stageId);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        await _viewModel.SaveAsync();
        await Shell.Current.GoToAsync("..");
    }
}
