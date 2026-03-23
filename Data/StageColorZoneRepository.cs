using HockeyTournamentTracker.Domain;
using SQLite;

namespace HockeyTournamentTracker.Data;

public sealed class StageColorZoneRepository : IStageColorZoneRepository
{
    private readonly SQLiteAsyncConnection _connection;

    public StageColorZoneRepository(LocalDatabase database)
    {
        _connection = database.Connection;
    }

    public async Task<IReadOnlyList<StageColorZone>> GetZonesByStageAsync(Guid stageId)
    {
        var rows = await _connection.Table<StageColorZoneEntity>()
            .Where(z => z.StageId == stageId)
            .ToListAsync();
        return rows
            .OrderBy(z => z.SortOrder)
            .ThenBy(z => z.Name, StringComparer.Ordinal)
            .Select(MapZone)
            .ToList();
    }

    public async Task<StageColorZone?> GetZoneByIdAsync(Guid zoneId)
    {
        var row = await _connection.FindAsync<StageColorZoneEntity>(zoneId);
        return row is null ? null : MapZone(row);
    }

    public async Task SaveZoneAsync(StageColorZone zone)
    {
        var entity = MapZoneEntity(zone);
        if (await _connection.FindAsync<StageColorZoneEntity>(entity.Id) is null)
            await _connection.InsertAsync(entity);
        else
            await _connection.UpdateAsync(entity);
    }

    public async Task DeleteZoneAsync(Guid zoneId)
    {
        await _connection.ExecuteAsync(
            "DELETE FROM StageTeamZones WHERE ZoneId = ?",
            zoneId);
        await _connection.DeleteAsync<StageColorZoneEntity>(zoneId);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetTeamZoneAssignmentsAsync(Guid stageId)
    {
        var rows = await _connection.Table<StageTeamZoneEntity>()
            .Where(t => t.StageId == stageId)
            .ToListAsync();
        return rows.ToDictionary(r => r.TeamId, r => r.ZoneId);
    }

    public async Task SetTeamZoneAsync(Guid stageId, Guid teamId, Guid? zoneId)
    {
        var existing = await _connection.Table<StageTeamZoneEntity>()
            .Where(t => t.StageId == stageId && t.TeamId == teamId)
            .FirstOrDefaultAsync();

        if (zoneId is null || zoneId == Guid.Empty)
        {
            if (existing is not null)
                await _connection.DeleteAsync(existing);
            return;
        }

        if (existing is null)
        {
            await _connection.InsertAsync(new StageTeamZoneEntity
            {
                Id = Guid.NewGuid(),
                StageId = stageId,
                TeamId = teamId,
                ZoneId = zoneId.Value
            });
        }
        else
        {
            existing.ZoneId = zoneId.Value;
            await _connection.UpdateAsync(existing);
        }
    }

    private static StageColorZone MapZone(StageColorZoneEntity e) =>
        new()
        {
            Id = e.Id,
            StageId = e.StageId,
            Name = e.Name,
            ColorHex = e.ColorHex,
            SortOrder = e.SortOrder
        };

    private static StageColorZoneEntity MapZoneEntity(StageColorZone z) =>
        new()
        {
            Id = z.Id == Guid.Empty ? Guid.NewGuid() : z.Id,
            StageId = z.StageId,
            Name = z.Name,
            ColorHex = string.IsNullOrWhiteSpace(z.ColorHex) ? "#808080" : z.ColorHex.Trim(),
            SortOrder = z.SortOrder
        };
}
