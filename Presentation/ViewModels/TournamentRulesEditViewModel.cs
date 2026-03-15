using System.ComponentModel;
using System.Runtime.CompilerServices;
using HockeyTournamentTracker.Data;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Presentation.ViewModels;

public sealed class TournamentRulesEditViewModel : INotifyPropertyChanged
{
    private readonly ITournamentRepository _tournamentRepository;

    private Guid _tournamentId;
    private string _pointsRegWin = "3";
    private string _pointsOtWin = "2";
    private string _pointsSoWin = "2";
    private string _pointsRegLoss = "0";
    private string _pointsOtLoss = "1";
    private string _pointsSoLoss = "1";

    public Guid TournamentId
    {
        get => _tournamentId;
        set => SetField(ref _tournamentId, value);
    }

    public string PointsRegWin { get => _pointsRegWin; set => SetField(ref _pointsRegWin, value); }
    public string PointsOtWin { get => _pointsOtWin; set => SetField(ref _pointsOtWin, value); }
    public string PointsSoWin { get => _pointsSoWin; set => SetField(ref _pointsSoWin, value); }
    public string PointsRegLoss { get => _pointsRegLoss; set => SetField(ref _pointsRegLoss, value); }
    public string PointsOtLoss { get => _pointsOtLoss; set => SetField(ref _pointsOtLoss, value); }
    public string PointsSoLoss { get => _pointsSoLoss; set => SetField(ref _pointsSoLoss, value); }

    public TournamentRulesEditViewModel(ITournamentRepository tournamentRepository)
    {
        _tournamentRepository = tournamentRepository;
    }

    public async Task LoadAsync()
    {
        if (TournamentId == Guid.Empty) return;
        var tournament = await _tournamentRepository.GetByIdAsync(TournamentId);
        if (tournament is null) return;
        var r = tournament.Rules;
        PointsRegWin = r.PointsForRegulationWin.ToString();
        PointsOtWin = r.PointsForOvertimeWin.ToString();
        PointsSoWin = r.PointsForShootoutWin.ToString();
        PointsRegLoss = r.PointsForRegulationLoss.ToString();
        PointsOtLoss = r.PointsForOvertimeLoss.ToString();
        PointsSoLoss = r.PointsForShootoutLoss.ToString();
    }

    public async Task<bool> SaveAsync()
    {
        if (TournamentId == Guid.Empty) return false;
        var tournament = await _tournamentRepository.GetByIdAsync(TournamentId);
        if (tournament is null) return false;

        tournament.Rules = new TournamentRules
        {
            PointsForRegulationWin = ParsePoints(PointsRegWin, 3),
            PointsForOvertimeWin = ParsePoints(PointsOtWin, 2),
            PointsForShootoutWin = ParsePoints(PointsSoWin, 2),
            PointsForRegulationLoss = ParsePoints(PointsRegLoss, 0),
            PointsForOvertimeLoss = ParsePoints(PointsOtLoss, 1),
            PointsForShootoutLoss = ParsePoints(PointsSoLoss, 1)
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
