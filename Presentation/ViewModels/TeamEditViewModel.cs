using System.ComponentModel;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class TeamEditViewModel : INotifyPropertyChanged
{
    private readonly ITeamRepository _teamRepository;

    private Guid _tournamentId;
    private Guid _teamId;
    private Guid _pendingTeamId; // для новой команды: Id под иконку
    private string _name = string.Empty;
    private string? _shortName;
    private string? _iconPath;

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

    public bool IsEditing => TeamId != Guid.Empty;

    public TeamEditViewModel(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task LoadAsync()
    {
        if (TeamId == Guid.Empty) return;

        var team = await _teamRepository.GetByIdAsync(TeamId);
        if (team is null) return;

        Name = team.Name;
        ShortName = team.ShortName;
        IconPath = team.IconPath;
    }

    public async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name) || TournamentId == Guid.Empty)
            return false;

        var id = TeamId != Guid.Empty ? TeamId : (_pendingTeamId != Guid.Empty ? _pendingTeamId : Guid.NewGuid());
        var team = new Team
        {
            Id = id,
            TournamentId = TournamentId,
            Name = Name.Trim(),
            ShortName = string.IsNullOrWhiteSpace(ShortName) ? null : ShortName.Trim(),
            IconPath = IconPath
        };

        await _teamRepository.SaveAsync(team);
        TeamId = id;
        return true;
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
