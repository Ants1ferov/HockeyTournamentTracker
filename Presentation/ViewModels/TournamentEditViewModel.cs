using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
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
    private string _pointsRegWin = "3";
    private string _pointsOtWin = "2";
    private string _pointsSoWin = "2";
    private string _pointsRegLoss = "0";
    private string _pointsOtLoss = "1";
    private string _pointsSoLoss = "1";

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

    public string PointsRegWin { get => _pointsRegWin; set => SetField(ref _pointsRegWin, value); }
    public string PointsOtWin { get => _pointsOtWin; set => SetField(ref _pointsOtWin, value); }
    public string PointsSoWin { get => _pointsSoWin; set => SetField(ref _pointsSoWin, value); }
    public string PointsRegLoss { get => _pointsRegLoss; set => SetField(ref _pointsRegLoss, value); }
    public string PointsOtLoss { get => _pointsOtLoss; set => SetField(ref _pointsOtLoss, value); }
    public string PointsSoLoss { get => _pointsSoLoss; set => SetField(ref _pointsSoLoss, value); }

    public ObservableCollection<StandingSortCriterion> SortOrderList { get; } =
        new(TournamentRules.GetDefaultSortOrder());

    public ICommand MoveSortUpCommand => new Command(OnMoveSortUp);
    public ICommand MoveSortDownCommand => new Command(OnMoveSortDown);

    public TournamentEditViewModel(ITournamentRepository tournamentRepository)
    {
        _tournamentRepository = tournamentRepository;
    }

    private void OnMoveSortUp(object? parameter)
    {
        if (parameter is StandingSortCriterion c)
        {
            var i = SortOrderList.IndexOf(c);
            if (i > 0)
                SortOrderList.Move(i, i - 1);
        }
    }

    private void OnMoveSortDown(object? parameter)
    {
        if (parameter is StandingSortCriterion c)
        {
            var i = SortOrderList.IndexOf(c);
            if (i >= 0 && i < SortOrderList.Count - 1)
                SortOrderList.Move(i, i + 1);
        }
    }

    public async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return false;
        }

        var rules = new TournamentRules
        {
            PointsForRegulationWin = ParsePoints(PointsRegWin, 3),
            PointsForOvertimeWin = ParsePoints(PointsOtWin, 2),
            PointsForShootoutWin = ParsePoints(PointsSoWin, 2),
            PointsForRegulationLoss = ParsePoints(PointsRegLoss, 0),
            PointsForOvertimeLoss = ParsePoints(PointsOtLoss, 1),
            PointsForShootoutLoss = ParsePoints(PointsSoLoss, 1),
            SortOrder = new List<StandingSortCriterion>(SortOrderList)
        };

        var tournament = new Tournament
        {
            Name = Name.Trim(),
            Description = Description,
            StartDate = StartDate,
            EndDate = EndDate,
            Status = TournamentStatus.Planned,
            Rules = rules
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

    private static int ParsePoints(string? value, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        return int.TryParse(value.Trim(), out var n) && n >= 0 ? n : defaultValue;
    }
}

