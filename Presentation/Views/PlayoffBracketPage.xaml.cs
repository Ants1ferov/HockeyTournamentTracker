using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Resources;
using System.Linq;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class PlayoffBracketPage : ContentPage, IQueryAttributable
{
    private readonly PlayoffBracketViewModel _viewModel;
    private Guid _tournamentId;
    private Guid _stageId;

    public PlayoffBracketPage(PlayoffBracketViewModel viewModel)
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
            _tournamentId = tournamentId;
            _stageId = stageId;
            await _viewModel.LoadAsync(_tournamentId, _stageId);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_tournamentId != Guid.Empty && _stageId != Guid.Empty)
            await _viewModel.LoadAsync(_tournamentId, _stageId);
    }

    private async void OnSaveSettingsClicked(object? sender, EventArgs e)
    {
        await _viewModel.SaveSettingsAsync();
    }

    private async void OnAddRoundClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Новый раунд", "Название раунда", initialValue: "");
        if (string.IsNullOrWhiteSpace(name))
            return;

        var bestOfInput = await DisplayPromptAsync("Новый раунд", "Best-of (нечётное число)", initialValue: "3", keyboard: Keyboard.Numeric);
        var bestOf = int.TryParse(bestOfInput, out var bo) ? bo : 3;

        await _viewModel.AddRoundAsync(name, bestOf);
    }

    private void OnRoundChipTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not PlayoffRoundUi round)
            return;
        _viewModel.SelectRound(round.Id);
    }

    private async void OnRenameSelectedRoundClicked(object? sender, EventArgs e)
    {
        if (_viewModel.SelectedRound is not PlayoffRoundUi round)
            return;

        var newName = await DisplayPromptAsync("Переименовать раунд", "Название", initialValue: round.Name);
        if (string.IsNullOrWhiteSpace(newName))
            return;

        await _viewModel.RenameRoundAsync(round.Id, newName);
    }

    private async void OnEditSelectedRoundBestOfClicked(object? sender, EventArgs e)
    {
        if (_viewModel.SelectedRound is not PlayoffRoundUi round)
            return;

        var value = await DisplayPromptAsync("Best-of раунда", "Введите нечётное число", initialValue: round.DefaultBestOf.ToString(), keyboard: Keyboard.Numeric);
        if (!int.TryParse(value, out var bestOf))
            return;

        await _viewModel.UpdateRoundBestOfAsync(round.Id, bestOf);
    }

    private async void OnDeleteSelectedRoundClicked(object? sender, EventArgs e)
    {
        if (_viewModel.SelectedRound is not PlayoffRoundUi round)
            return;

        var ok = await DisplayAlert(AppResources.Delete, $"Удалить раунд «{round.Name}»?", AppResources.Ok, AppResources.Cancel);
        if (!ok)
            return;

        await _viewModel.DeleteRoundAsync(round.Id);
    }

    private async void OnAddSeriesInSelectedRoundClicked(object? sender, EventArgs e)
    {
        if (_viewModel.SelectedRound is not PlayoffRoundUi round)
            return;

        await _viewModel.AddSeriesAsync(round.Id);
    }

    private async void OnEditSeriesClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not PlayoffSeriesUi series)
            return;

        var selectedHome = await PromptTeamAsync("Выберите домашнюю команду", series.HomeTeamId);
        if (selectedHome.Cancelled)
            return;

        var selectedAway = await PromptTeamAsync("Выберите гостевую команду", series.AwayTeamId);
        if (selectedAway.Cancelled)
            return;

        var bestOfPrompt = await DisplayPromptAsync(
            "Best-of серии",
            "Оставьте пустым для значения раунда",
            initialValue: series.BestOfOverride?.ToString() ?? string.Empty,
            keyboard: Keyboard.Numeric);

        int? bestOfOverride = null;
        if (!string.IsNullOrWhiteSpace(bestOfPrompt) && int.TryParse(bestOfPrompt, out var parsed))
            bestOfOverride = parsed;

        var ok = await _viewModel.UpdateSeriesAsync(series.Id, selectedHome.TeamId, selectedAway.TeamId, bestOfOverride);
        if (!ok)
            await DisplayAlert(AppResources.Error, "Нельзя выбрать одну и ту же команду дважды.", AppResources.Ok);
    }

    private async void OnDeleteSeriesClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not PlayoffSeriesUi series)
            return;

        var ok = await DisplayAlert(AppResources.Delete, "Удалить серию?", AppResources.Ok, AppResources.Cancel);
        if (!ok)
            return;

        await _viewModel.DeleteSeriesAsync(series.Id);
    }

    private async void OnAddSeriesMatchClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not PlayoffSeriesUi series)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(MatchEditPage)}?TournamentId={_tournamentId}&StageId={_stageId}&SeriesId={series.Id}");
    }

    private async void OnOpenSeriesMatchesClicked(object? sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not PlayoffSeriesUi series)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(StageMatchesPage)}?TournamentId={_tournamentId}&StageId={_stageId}&SeriesId={series.Id}");
    }

    private async void OnAutoFillSeedClicked(object? sender, EventArgs e)
    {
        if (SeedSourceStagePicker.SelectedItem is not Stage sourceStage)
        {
            await DisplayAlert(AppResources.Error, "Выберите стадию для посева.", AppResources.Ok);
            return;
        }

        var ok = await _viewModel.AutoFillFirstRoundFromStageAsync(sourceStage.Id);
        if (!ok)
            await DisplayAlert(AppResources.Error, "Не удалось автозаполнить сетку. Проверьте раунды/серии и данные стадии.", AppResources.Ok);
    }

    private async Task<(Guid? TeamId, bool Cancelled)> PromptTeamAsync(string title, Guid? current)
    {
        var options = new List<(string Label, Guid? TeamId)> { ("—", null) };
        options.AddRange(_viewModel.Teams.Select(t => (t.Name, (Guid?)t.Id)));

        var cancel = AppResources.Cancel;
        var currentLabel = current.HasValue
            ? _viewModel.Teams.FirstOrDefault(t => t.Id == current.Value)?.Name ?? "—"
            : "—";

        var selected = await DisplayActionSheet(title, cancel, null, options.Select(o => o.Label).ToArray());
        if (selected == cancel)
            return (current, true);

        var found = options.FirstOrDefault(o => o.Label == selected);
        return (found.TeamId, false);
    }
}
