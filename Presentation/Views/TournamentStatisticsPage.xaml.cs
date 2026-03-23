using HockeyTournamentTracker.Presentation.ViewModels;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class TournamentStatisticsPage : ContentPage, IQueryAttributable
{
    private readonly TournamentStatisticsViewModel _viewModel;

    public TournamentStatisticsPage(TournamentStatisticsViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("TournamentId", out var v) && v is string s && Guid.TryParse(s, out var id))
            await _viewModel.LoadAsync(id);
    }
}
