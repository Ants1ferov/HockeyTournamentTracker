using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class TeamEditViewModel : INotifyPropertyChanged
{
    private readonly ITeamRepository _teamRepository;
    private readonly ITournamentRepository _tournamentRepository;

    private Guid _tournamentId;
    private Guid _teamId;
    private Guid _pendingTeamId; // для новой команды: Id под иконку
    private bool _isSaving;
    private string _name = string.Empty;
    private string? _shortName;
    private string? _iconPath;
    private GroupInfo? _selectedGroup;

    public Guid TournamentId
    {
        get => _tournamentId;
        set => SetField(ref _tournamentId, value);
    }

    public Guid TeamId
    {
        get => _teamId;
        set => SetField(ref _teamId, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value ?? string.Empty);
    }

    public string? ShortName
    {
        get => _shortName;
        set => SetField(ref _shortName, value);
    }

    public string? IconPath
    {
        get => _iconPath;
        set => SetField(ref _iconPath, value);
    }

    public ObservableCollection<GroupInfo> Groups { get; } = new();

    public GroupInfo? SelectedGroup
    {
        get => _selectedGroup;
        set => SetField(ref _selectedGroup, value);
    }

    public bool IsEditing => TeamId != Guid.Empty;

    public bool IsSaving => _isSaving;

    public bool CanSave => !_isSaving;

    public TeamEditViewModel(ITeamRepository teamRepository, ITournamentRepository tournamentRepository)
    {
        _teamRepository = teamRepository;
        _tournamentRepository = tournamentRepository;
    }

    public async Task LoadGroupsAsync()
    {
        Groups.Clear();
        if (TournamentId == Guid.Empty) return;
        var tournament = await _tournamentRepository.GetByIdAsync(TournamentId);
        if (tournament?.Rules.Groups == null) return;
        Groups.Add(new GroupInfo { Id = Guid.Empty, Name = "—", Order = -1 });
        foreach (var g in tournament.Rules.Groups.OrderBy(x => x.Order).ThenBy(x => x.Name))
            Groups.Add(g);
    }

    public async Task LoadAsync()
    {
        await LoadGroupsAsync();
        if (TeamId == Guid.Empty) return;

        var team = await _teamRepository.GetByIdAsync(TeamId);
        if (team is null) return;

        Name = team.Name;
        ShortName = team.ShortName;
        IconPath = team.IconPath;
        SelectedGroup = team.GroupId is { } gid
            ? Groups.FirstOrDefault(g => g.Id == gid)
            : Groups.FirstOrDefault(g => g.Id == Guid.Empty);
    }

    public async Task<bool> SaveAsync()
    {
        if (_isSaving || string.IsNullOrWhiteSpace(Name) || TournamentId == Guid.Empty)
            return false;
        _isSaving = true;
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(CanSave));
        try
        {
            var id = TeamId != Guid.Empty ? TeamId : (_pendingTeamId != Guid.Empty ? _pendingTeamId : Guid.NewGuid());
            var groupId = SelectedGroup is { } g && g.Id != Guid.Empty ? g.Id : (Guid?)null;
            var team = new Team
            {
                Id = id,
                TournamentId = TournamentId,
                Name = Name.Trim(),
                ShortName = string.IsNullOrWhiteSpace(ShortName) ? null : ShortName.Trim(),
                IconPath = IconPath,
                GroupId = groupId
            };

            await _teamRepository.SaveAsync(team);
            TeamId = id;
            return true;
        }
        finally
        {
            _isSaving = false;
            OnPropertyChanged(nameof(IsSaving));
            OnPropertyChanged(nameof(CanSave));
        }
    }

    public async Task PickIconAsync()
    {
        try
        {
            var photo = await MediaPicker.PickPhotoAsync(new MediaPickerOptions { Title = "Выберите иконку команды" });
            if (photo is null) return;

            var iconId = TeamId != Guid.Empty ? TeamId : (_pendingTeamId == Guid.Empty ? Guid.NewGuid() : _pendingTeamId);
            if (TeamId == Guid.Empty && _pendingTeamId == Guid.Empty)
                _pendingTeamId = iconId;

            await using var stream = await photo.OpenReadAsync();
            var path = TeamIconHelper.GetIconPath(TournamentId, iconId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var fileStream = File.Create(path);
            await stream.CopyToAsync(fileStream);
            IconPath = path;
        }
        catch
        {
            // Пользователь отменил или ошибка — игнорируем
        }
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
