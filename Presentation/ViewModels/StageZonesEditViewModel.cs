using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class StageZonesEditViewModel : INotifyPropertyChanged
{
    private readonly IStageColorZoneRepository _zoneRepository;
    private readonly IStageTeamRepository _stageTeamRepository;
    private readonly ITeamRepository _teamRepository;

    private Guid _tournamentId;
    private Guid _stageId;

    /// <summary>Пресеты цветов зон (название, hex). Индексы синхронны с пикером.</summary>
    public static readonly (string Name, string Hex)[] ColorPresets =
    {
        ("Плей-офф (зелёная)", "#1D9E75"),
        ("Стыки (оранжевая)", "#BA7517"),
        ("Вне (красная)", "#E24B4A"),
        ("Нейтраль (серая)", "#C8C8C8")
    };

    public List<string> ColorPresetNames { get; } = ColorPresets.Select(p => p.Name).ToList();

    public ObservableCollection<ZoneEditItem> Zones { get; } = new();
    public ObservableCollection<TeamZoneEditItem> Teams { get; } = new();
    /// <summary>Имена зон для пикеров команд: индекс 0 = «— нет —».</summary>
    public ObservableCollection<string> ZoneNames { get; } = new();

    public ICommand AddZoneCommand { get; }
    public ICommand DeleteZoneCommand { get; }

    public StageZonesEditViewModel(
        IStageColorZoneRepository zoneRepository,
        IStageTeamRepository stageTeamRepository,
        ITeamRepository teamRepository)
    {
        _zoneRepository = zoneRepository;
        _stageTeamRepository = stageTeamRepository;
        _teamRepository = teamRepository;

        AddZoneCommand = new Command(AddZone);
        DeleteZoneCommand = new Command<ZoneEditItem>(DeleteZone);
    }

    public async Task LoadAsync(Guid tournamentId, Guid stageId)
    {
        _tournamentId = tournamentId;
        _stageId = stageId;

        var zones = await _zoneRepository.GetZonesByStageAsync(stageId);
        var assignments = await _zoneRepository.GetTeamZoneAssignmentsAsync(stageId);
        var teamIds = (await _stageTeamRepository.GetTeamIdsByStageAsync(stageId)).ToHashSet();
        var allTeams = await _teamRepository.GetByTournamentAsync(tournamentId);
        var teamsInStage = allTeams.Where(t => teamIds.Contains(t.Id)).OrderBy(t => t.Name).ToList();

        Zones.Clear();
        foreach (var z in zones)
            Zones.Add(new ZoneEditItem
            {
                Id = z.Id,
                Name = z.Name,
                ColorIndex = FindPresetIndex(z.ColorHex)
            });
        RebuildZoneNames();

        Teams.Clear();
        foreach (var t in teamsInStage)
        {
            var idx = 0;
            if (assignments.TryGetValue(t.Id, out var zid))
            {
                var zi = Zones.ToList().FindIndex(z => z.Id == zid);
                idx = zi >= 0 ? zi + 1 : 0;
            }
            Teams.Add(new TeamZoneEditItem
            {
                TeamId = t.Id,
                Name = t.Name,
                IconPath = t.IconPath,
                SelectedZoneIndex = idx
            });
        }
    }

    private static int FindPresetIndex(string hex)
    {
        for (var i = 0; i < ColorPresets.Length; i++)
            if (string.Equals(ColorPresets[i].Hex, hex, StringComparison.OrdinalIgnoreCase))
                return i;
        return 0;
    }

    private void RebuildZoneNames()
    {
        ZoneNames.Clear();
        ZoneNames.Add("— нет —");
        foreach (var z in Zones)
            ZoneNames.Add(string.IsNullOrWhiteSpace(z.Name) ? "(без имени)" : z.Name);
    }

    private void AddZone()
    {
        var index = Zones.Count % ColorPresets.Length;
        var name = ColorPresets[index].Name.Split(' ')[0];
        Zones.Add(new ZoneEditItem
        {
            Id = Guid.NewGuid(),
            Name = name,
            ColorIndex = index
        });
        // Важно: НЕ пересобираем весь список (Clear() сбросил бы выбор пикеров
        // у команд). Дописываем имя новой зоны в конец — индексы прежних зон
        // не меняются, выбранные значения сохраняются.
        ZoneNames.Add(string.IsNullOrWhiteSpace(name) ? "(без имени)" : name);
    }

    private void DeleteZone(ZoneEditItem? zone)
    {
        if (zone is null) return;

        // Снимок выбранной зоны по Guid ДО изменения списка (индексы поедут).
        var selectedZoneIdByTeam = new Dictionary<Guid, Guid?>();
        foreach (var t in Teams)
        {
            Guid? zid = t.SelectedZoneIndex > 0 && t.SelectedZoneIndex <= Zones.Count
                ? Zones[t.SelectedZoneIndex - 1].Id
                : null;
            selectedZoneIdByTeam[t.TeamId] = zid;
        }

        Zones.Remove(zone);
        RebuildZoneNames();

        // Восстановить выбор по Guid: удалённая зона → «нет», иначе новый индекс.
        foreach (var t in Teams)
        {
            var zid = selectedZoneIdByTeam.TryGetValue(t.TeamId, out var z) ? z : null;
            var newIndex = 0;
            if (zid is { } id)
            {
                var pos = Zones.ToList().FindIndex(zz => zz.Id == id);
                newIndex = pos >= 0 ? pos + 1 : 0;
            }
            t.SelectedZoneIndex = newIndex;
        }
    }

    public async Task<bool> SaveAsync()
    {
        // Удалить из БД зоны, которых больше нет в списке.
        var existing = await _zoneRepository.GetZonesByStageAsync(_stageId);
        var keepIds = Zones.Select(z => z.Id).ToHashSet();
        foreach (var ex in existing)
            if (!keepIds.Contains(ex.Id))
                await _zoneRepository.DeleteZoneAsync(ex.Id);

        // Сохранить/обновить зоны.
        var order = 0;
        foreach (var z in Zones)
        {
            await _zoneRepository.SaveZoneAsync(new StageColorZone
            {
                Id = z.Id,
                StageId = _stageId,
                Name = string.IsNullOrWhiteSpace(z.Name) ? "Зона" : z.Name.Trim(),
                ColorHex = ColorPresets[Math.Clamp(z.ColorIndex, 0, ColorPresets.Length - 1)].Hex,
                SortOrder = order++
            });
        }

        // Сохранить назначения команд.
        foreach (var t in Teams)
        {
            Guid? zoneId = t.SelectedZoneIndex > 0 && t.SelectedZoneIndex <= Zones.Count
                ? Zones[t.SelectedZoneIndex - 1].Id
                : null;
            await _zoneRepository.SetTeamZoneAsync(_stageId, t.TeamId, zoneId);
        }

        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class ZoneEditItem : INotifyPropertyChanged
{
    public Guid Id { get; set; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    private int _colorIndex;
    public int ColorIndex
    {
        get => _colorIndex;
        set { _colorIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(ColorHex)); }
    }

    public string ColorHex =>
        StageZonesEditViewModel.ColorPresets[Math.Clamp(_colorIndex, 0, StageZonesEditViewModel.ColorPresets.Length - 1)].Hex;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class TeamZoneEditItem : INotifyPropertyChanged
{
    public Guid TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconPath { get; set; }

    private int _selectedZoneIndex;
    public int SelectedZoneIndex
    {
        get => _selectedZoneIndex;
        set { _selectedZoneIndex = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
