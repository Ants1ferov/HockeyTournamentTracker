using System.ComponentModel;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class TournamentEditViewModel : INotifyPropertyChanged
{
    private readonly ITournamentRepository _tournamentRepository;

    private string _name = string.Empty;
    private string? _description;
    private DateTime? _startDate = DateTime.Today;
    private DateTime? _endDate;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string? Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public DateTime? StartDate
    {
        get => _startDate;
        set => SetField(ref _startDate, value);
    }

    public DateTime? EndDate
    {
        get => _endDate;
        set => SetField(ref _endDate, value);
    }

    public TournamentEditViewModel(ITournamentRepository tournamentRepository)
    {
        _tournamentRepository = tournamentRepository;
    }

    public async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return false;
        }

        var tournament = new Tournament
        {
            Name = Name.Trim(),
            Description = Description,
            StartDate = StartDate,
            EndDate = EndDate,
            Status = TournamentStatus.Planned,
            Rules = new TournamentRules()
        };

        await _tournamentRepository.SaveAsync(tournament);
        return true;
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

