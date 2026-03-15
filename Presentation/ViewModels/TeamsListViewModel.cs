using System.Collections.ObjectModel;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class TeamsListViewModel
{
    private readonly ITeamRepository _teamRepository;
    private bool _isLoading;

    public Guid TournamentId { get; set; }
    public ObservableCollection<Team> Teams { get; } = new();

    public TeamsListViewModel(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task LoadAsync()
    {
        if (_isLoading || TournamentId == Guid.Empty)
            return;
        _isLoading = true;
        try
        {
            Teams.Clear();
            var list = await _teamRepository.GetByTournamentAsync(TournamentId);
            foreach (var team in list)
                Teams.Add(team);
        }
        finally
        {
            _isLoading = false;
        }
    }

    public async Task DeleteTeamAsync(Team team)
    {
        await _teamRepository.DeleteAsync(team.Id);
        Teams.Remove(team);
    }
}
