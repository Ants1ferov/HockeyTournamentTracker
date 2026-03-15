using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Presentation.ViewModels;
using HockeyTournamentTracker.Resources;

namespace HockeyTournamentTracker.Presentation.Views;

public partial class GroupsListPage : ContentPage, IQueryAttributable
{
    private readonly GroupsListViewModel _viewModel;

    public GroupsListPage(GroupsListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("TournamentId", out var value) && value is string idString &&
            Guid.TryParse(idString, out var id))
        {
            _viewModel.TournamentId = id;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnAddGroupClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync(AppResources.Groups, AppResources.GroupName, initialValue: "");
        if (!string.IsNullOrWhiteSpace(name))
            await _viewModel.AddGroupAsync(name);
    }

    private async void OnDeleteGroupClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not GroupInfo group)
            return;
        var ok = await DisplayAlert(AppResources.Delete, $"{AppResources.DeleteTeamConfirm} \"{group.Name}\"?", AppResources.Ok, AppResources.Cancel);
        if (ok)
            await _viewModel.DeleteGroupAsync(group);
    }

    private async void OnAllowCrossGroupToggled(object? sender, ToggledEventArgs e)
    {
        await _viewModel.SaveAllowCrossGroupAsync();
    }
}
