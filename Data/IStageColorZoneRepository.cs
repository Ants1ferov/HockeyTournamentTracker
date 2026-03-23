using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Data;

public interface IStageColorZoneRepository
{
    Task<IReadOnlyList<StageColorZone>> GetZonesByStageAsync(Guid stageId);
    Task<StageColorZone?> GetZoneByIdAsync(Guid zoneId);
    Task SaveZoneAsync(StageColorZone zone);
    Task DeleteZoneAsync(Guid zoneId);

    /// <summary>TeamId -> ZoneId для стадии (только назначенные).</summary>
    Task<IReadOnlyDictionary<Guid, Guid>> GetTeamZoneAssignmentsAsync(Guid stageId);

    Task SetTeamZoneAsync(Guid stageId, Guid teamId, Guid? zoneId);
}
