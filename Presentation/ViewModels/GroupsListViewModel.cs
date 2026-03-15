using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class GroupsListViewModel : INotifyPropertyChanged
{
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ITeamRepository _teamRepository;

    private Guid _tournamentId;
    private bool _allowCrossGroupMatches;

    public Guid TournamentId
    {
        get => _tournamentId;
        set => SetField(ref _tournamentId, value);
    }

    public bool AllowCrossGroupMatches
    {
        get => _allowCrossGroupMatches;
        set => SetField(ref _allowCrossGroupMatches, value);
    }

    public ObservableCollection<GroupInfo> GroupsList { get; } = new();

    public GroupsListViewModel(ITournamentRepository tournamentRepository, ITeamRepository teamRepository)
    {
        _tournamentRepository = tournamentRepository;
        _teamRepository = teamRepository;
    }

    public async Task LoadAsync()
    {
        if (TournamentId == Guid.Empty) return;
        var tournament = await _tournamentRepository.GetByIdAsync(TournamentId);
        if (tournament is null) return;
        var rules = tournament.Rules;
        AllowCrossGroupMatches = rules.AllowCrossGroupMatches;
        GroupsList.Clear();
        if (rules.Groups != null)
        {
            foreach (var g in rules.Groups.OrderBy(x => x.Order).ThenBy(x => x.Name))
                GroupsList.Add(g);
        }
    }

    public async Task AddGroupAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || TournamentId == Guid.Empty) return;
        var tournament = await _tournamentRepository.GetByIdAsync(TournamentId);
        if (tournament is null) return;
        var order = GroupsList.Count;
        var newGroup = new GroupInfo { Id = Guid.NewGuid(), Name = name.Trim(), Order = order };
        GroupsList.Add(newGroup);
        await SaveRulesAsync();
    }

    public async Task UpdateGroupNameAsync(GroupInfo group, string newName)
    {
        if (group is null || string.IsNullOrWhiteSpace(newName)) return;
        var idx = GroupsList.IndexOf(group);
        if (idx < 0) return;
        GroupsList.RemoveAt(idx);
        GroupsList.Insert(idx, new GroupInfo { Id = group.Id, Name = newName.Trim(), Order = group.Order });
        await SaveRulesAsync();
    }

    public async Task DeleteGroupAsync(GroupInfo group)
    {
        if (group is null) return;
        GroupsList.Remove(group);
        var teams = await _teamRepository.GetByTournamentAsync(TournamentId);
        foreach (var t in teams.Where(t => t.GroupId == group.Id))
        {
            t.GroupId = null;
            await _teamRepository.SaveAsync(t);
        }
        await SaveRulesAsync();
    }

    public async Task SaveAllowCrossGroupAsync()
    {
        await SaveRulesAsync();
    }

    private async Task SaveRulesAsync()
    {
        if (TournamentId == Guid.Empty) return;
        var tournament = await _tournamentRepository.GetByIdAsync(TournamentId);
        if (tournament is null) return;
        var r = tournament.Rules;
        tournament.Rules = new TournamentRules
        {
            PointsForRegulationWin = r.PointsForRegulationWin,
            PointsForOvertimeWin = r.PointsForOvertimeWin,
            PointsForShootoutWin = r.PointsForShootoutWin,
            PointsForRegulationLoss = r.PointsForRegulationLoss,
            PointsForOvertimeLoss = r.PointsForOvertimeLoss,
            PointsForShootoutLoss = r.PointsForShootoutLoss,
            SortOrder = r.SortOrder is { Count: > 0 } ? r.SortOrder : TournamentRules.GetDefaultSortOrder(),
            Groups = new List<GroupInfo>(GroupsList),
            AllowCrossGroupMatches = AllowCrossGroupMatches
        };
        await _tournamentRepository.SaveAsync(tournament);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
