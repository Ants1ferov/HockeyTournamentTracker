using System.ComponentModel;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class StageEditViewModel : INotifyPropertyChanged
{
    private readonly IStageRepository _stageRepository;

    private Guid _tournamentId;
    private Guid _stageId;
    private string _name = string.Empty;
    private int _order;
    private int _stageTypeIndex;

    public Guid TournamentId
    {
        get => _tournamentId;
        set => SetField(ref _tournamentId, value);
    }

    public Guid StageId
    {
        get => _stageId;
        set => SetField(ref _stageId, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value ?? string.Empty);
    }

    public int Order
    {
        get => _order;
        set => SetField(ref _order, value);
    }

    public int StageTypeIndex
    {
        get => _stageTypeIndex;
        set => SetField(ref _stageTypeIndex, value);
    }

    public StageEditViewModel(IStageRepository stageRepository)
    {
        _stageRepository = stageRepository;
    }

    public async Task LoadAsync(Guid tournamentId, Guid? stageId = null)
    {
        TournamentId = tournamentId;
        if (stageId is { } id)
        {
            var stage = await _stageRepository.GetByIdAsync(id);
            if (stage is not null)
            {
                StageId = stage.Id;
                Name = stage.Name;
                Order = stage.Order;
                StageTypeIndex = (int)stage.StageType;
            }
        }
        else
        {
            StageId = Guid.Empty;
            var stages = await _stageRepository.GetByTournamentAsync(tournamentId);
            Order = stages.Count > 0 ? stages.Max(s => s.Order) + 1 : 0;
            StageTypeIndex = 0;
        }
    }

    public async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name) || TournamentId == Guid.Empty)
            return;
        var stage = new Stage
        {
            Id = StageId == Guid.Empty ? Guid.NewGuid() : StageId,
            TournamentId = TournamentId,
            Name = Name.Trim(),
            Order = Order,
            StageType = StageTypeIndex switch { 1 => StageType.PlayOff, _ => StageType.Swiss }
        };
        await _stageRepository.SaveAsync(stage);
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
