namespace HockeyTournamentTracker.Domain;

/// <summary>Именованная цветовая зона для стадии (швейцарская система).</summary>
public sealed class StageColorZone
{
    public Guid Id { get; set; }
    public Guid StageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#808080";
    public int SortOrder { get; set; }
}
