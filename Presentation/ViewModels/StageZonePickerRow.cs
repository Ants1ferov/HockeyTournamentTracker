namespace HockeyTournamentTracker.Presentation.ViewModels;

/// <summary>Элемент Picker назначения цветовой зоны: null — без зоны.</summary>
public sealed class StageZonePickerRow
{
    public Guid? ZoneId { get; set; }
    public string Title { get; set; } = string.Empty;
}

public sealed class StageZoneLegendItem
{
    public string Name { get; }
    public string ColorHex { get; }

    public StageZoneLegendItem(string name, string colorHex)
    {
        Name = name;
        ColorHex = colorHex;
    }
}
