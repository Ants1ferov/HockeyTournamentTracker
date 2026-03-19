using System.Linq;
using SQLite;
using HockeyTournamentTracker.Domain;

namespace HockeyTournamentTracker.Data;

public interface IStageTeamRepository
{
    Task<IReadOnlyList<Guid>> GetTeamIdsByStageAsync(Guid stageId);
    Task<IReadOnlyDictionary<Guid, Guid?>> GetTeamGroupIdsByStageAsync(Guid stageId);
    Task AddTeamsToStageAsync(Guid stageId, IReadOnlyList<Guid> teamIds);
    Task AddTeamsToStageAsync(Guid stageId, IReadOnlyDictionary<Guid, Guid?> teamGroupByTeamId);
    Task<bool> IsTeamInStageAsync(Guid stageId, Guid teamId);
    Task SetTeamsGroupIdAsync(Guid stageId, IReadOnlyList<Guid> teamIds, Guid? groupId);
}

public sealed class StageTeamRepository : IStageTeamRepository
{
    private readonly SQLiteAsyncConnection _connection;

    public StageTeamRepository(LocalDatabase database)
    {
        _connection = database.Connection;
    }

    public async Task<IReadOnlyList<Guid>> GetTeamIdsByStageAsync(Guid stageId)
    {
        var rows = await _connection.Table<StageTeamEntity>()
            .Where(st => st.StageId == stageId)
            .ToListAsync();

        return rows.Select(r => r.TeamId).Distinct().ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, Guid?>> GetTeamGroupIdsByStageAsync(Guid stageId)
    {
        // StageTeams является таблицей "многие-ко-многим": (StageId, TeamId) -> GroupId.
        // GroupId может быть null (Без группы).
        var rows = await _connection.Table<StageTeamEntity>()
            .Where(st => st.StageId == stageId)
            .ToListAsync();

        return rows
            .GroupBy(r => r.TeamId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.GroupId).FirstOrDefault());
    }

    public async Task<bool> IsTeamInStageAsync(Guid stageId, Guid teamId)
    {
        var entity = await _connection.Table<StageTeamEntity>()
            .FirstOrDefaultAsync(st => st.StageId == stageId && st.TeamId == teamId);
        return entity is not null;
    }

    public async Task AddTeamsToStageAsync(Guid stageId, IReadOnlyList<Guid> teamIds)
    {
        if (teamIds.Count == 0)
            return;

        // Убираем дубликаты на входе
        var distinct = teamIds.Distinct().ToList();

        var existing = await _connection.Table<StageTeamEntity>()
            .Where(st => st.StageId == stageId && distinct.Contains(st.TeamId))
            .ToListAsync();

        var existingTeamIds = existing.Select(e => e.TeamId).ToHashSet();

        var toInsert = distinct.Where(id => !existingTeamIds.Contains(id)).ToList();
        foreach (var teamId in toInsert)
        {
            await _connection.InsertAsync(new StageTeamEntity
            {
                Id = Guid.NewGuid(),
                StageId = stageId,
                TeamId = teamId,
                GroupId = null
            });
        }
    }

    public async Task AddTeamsToStageAsync(Guid stageId, IReadOnlyDictionary<Guid, Guid?> teamGroupByTeamId)
    {
        if (teamGroupByTeamId.Count == 0)
            return;

        // Убираем дубликаты входных ключей.
        var distinct = teamGroupByTeamId
            .Where(kv => kv.Key != Guid.Empty)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var teamIds = distinct.Keys.ToList();

        var existing = await _connection.Table<StageTeamEntity>()
            .Where(st => st.StageId == stageId && teamIds.Contains(st.TeamId))
            .ToListAsync();

        var existingTeamIds = existing.Select(e => e.TeamId).ToHashSet();
        var toInsert = distinct.Where(kv => !existingTeamIds.Contains(kv.Key)).ToList();

        foreach (var (teamId, groupId) in toInsert)
        {
            await _connection.InsertAsync(new StageTeamEntity
            {
                Id = Guid.NewGuid(),
                StageId = stageId,
                TeamId = teamId,
                GroupId = groupId
            });
        }
    }

    public async Task SetTeamsGroupIdAsync(Guid stageId, IReadOnlyList<Guid> teamIds, Guid? groupId)
    {
        if (teamIds.Count == 0)
            return;

        var distinctIds = teamIds.Distinct().ToList();
        var entities = await _connection.Table<StageTeamEntity>()
            .Where(st => st.StageId == stageId && distinctIds.Contains(st.TeamId))
            .ToListAsync();

        foreach (var e in entities)
            e.GroupId = groupId;

        // SQLite-net не имеет batch-update в одну операцию, поэтому обновляем по месту.
        foreach (var e in entities)
            await _connection.UpdateAsync(e);
    }
}

