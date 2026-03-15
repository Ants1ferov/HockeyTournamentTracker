using HockeyTournamentTracker.Presentation.ViewModels;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class StageEditPage : ContentPage, IQueryAttributable
{
    private readonly StageEditViewModel _viewModel;

    public StageEditPage(StageEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("TournamentId", out var tVal) || tVal is not string tStr || !Guid.TryParse(tStr, out var tournamentId))
            return;
        Guid? stageId = null;
        if (query.TryGetValue("StageId", out var sVal) && sVal is string sStr && Guid.TryParse(sStr, out var sid))
            stageId = sid;
        await _viewModel.LoadAsync(tournamentId, stageId);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.TournamentId != Guid.Empty && _viewModel.StageId != Guid.Empty)
            await _viewModel.LoadAsync(_viewModel.TournamentId, _viewModel.StageId);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        await _viewModel.SaveAsync();
        await Shell.Current.GoToAsync("..");
    }
}
