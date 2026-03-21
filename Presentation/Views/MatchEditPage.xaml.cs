using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Resources;

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
        }

        if (query.TryGetValue("StageId", out var sVal) && sVal is string sStr && Guid.TryParse(sStr, out var stageId))
            _viewModel.StageId = stageId;
        else
            _viewModel.StageId = null;

        if (query.TryGetValue("SeriesId", out var seriesVal) && seriesVal is string seriesStr && Guid.TryParse(seriesStr, out var seriesId))
            _viewModel.SeriesId = seriesId;
        else
            _viewModel.SeriesId = null;

        if (_viewModel.TournamentId != Guid.Empty)
            await _viewModel.LoadTeamsAsync();

        if (query.TryGetValue("MatchId", out var mVal) && mVal is string mStr && Guid.TryParse(mStr, out var matchId))
        {
            _viewModel.MatchId = matchId;
            Title = "Редактирование матча";
            await _viewModel.LoadMatchAsync();
        }
        else
        {
            _viewModel.MatchId = Guid.Empty;
            Title = AppResources.NewMatch;
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

    private async void OnSelectHomeTeamClicked(object? sender, EventArgs e)
    {
        var selected = await PromptTeamAsync(AppResources.HomeTeam, _viewModel.HomeTeam, _viewModel.Teams);
        if (selected is not null)
            _viewModel.HomeTeam = selected;
    }

    private async void OnSelectAwayTeamClicked(object? sender, EventArgs e)
    {
        var selected = await PromptTeamAsync(AppResources.AwayTeam, _viewModel.AwayTeam, _viewModel.AwayTeamOptions);
        if (selected is not null)
            _viewModel.AwayTeam = selected;
    }

    private async Task<Team?> PromptTeamAsync(string title, Team? current, IEnumerable<Team> options)
    {
        var teams = options.ToList();
        if (teams.Count == 0)
        {
            await DisplayAlert(AppResources.Error, "Нет доступных команд.", AppResources.Ok);
            return null;
        }

        var cancel = AppResources.Cancel;
        var labels = teams.Select(t => t.Name).ToArray();
        var selected = await DisplayActionSheet(title, cancel, null, labels);
        if (selected == cancel)
            return current;

        return teams.FirstOrDefault(t => string.Equals(t.Name, selected, StringComparison.Ordinal)) ?? current;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var success = await _viewModel.SaveAsync();
        if (!success)
        {
            await DisplayAlert(AppResources.Error, AppResources.ErrorCheckTeamsAndScore, AppResources.Ok);
            return;
        }

        await Shell.Current.GoToAsync("..");
    }
}

