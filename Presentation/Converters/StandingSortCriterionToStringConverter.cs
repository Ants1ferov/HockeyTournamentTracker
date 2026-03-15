using System.Globalization;
using HockeyTournamentTracker.Domain;
using HockeyTournamentTracker.Resources;

namespace HockeyTournamentTracker.Presentation.Converters;

public sealed class StandingSortCriterionToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not StandingSortCriterion criterion)
            return string.Empty;
        return criterion switch
        {
            StandingSortCriterion.Points => AppResources.SortCriterionPoints,
            StandingSortCriterion.WinsRegulation => AppResources.SortCriterionWinsRegulation,
            StandingSortCriterion.WinsOvertime => AppResources.SortCriterionWinsOvertime,
            StandingSortCriterion.WinsShootout => AppResources.SortCriterionWinsShootout,
            StandingSortCriterion.GoalDifference => AppResources.SortCriterionGoalDifference,
            StandingSortCriterion.GoalsFor => AppResources.SortCriterionGoalsFor,
            StandingSortCriterion.GoalsAgainst => AppResources.SortCriterionGoalsAgainst,
            StandingSortCriterion.Games => AppResources.SortCriterionGames,
            _ => value.ToString() ?? string.Empty
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
