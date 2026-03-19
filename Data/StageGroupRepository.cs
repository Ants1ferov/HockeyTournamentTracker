using HockeyTournamentTracker.Domain;
using SQLite;

namespace HockeyTournamentTracker.Data;

public sealed class StageGroupRepository : IStageGroupRepository
{
    private readonly SQLiteAsyncConnection _connection;

    public StageGroupRepository(LocalDatabase database)
    {
        _connection = database.Connection;
    }

    public async Task<IReadOnlyList<GroupInfo>> GetByStageAsync(Guid stageId)
    {
        var rows = await _connection.Table<StageGroupEntity>()
            .Where(g => g.StageId == stageId)
            .OrderBy(g => g.Order)
            .ThenBy(g => g.Name)
            .ToListAsync();

        return rows.Select(MapToDomain).ToList();
    }

    public async Task<GroupInfo?> AddGroupAsync(Guid stageId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            return null;

        var order = await _connection.Table<StageGroupEntity>()
            .Where(g => g.StageId == stageId)
            .ToListAsync();
        var newGroup = new StageGroupEntity
        {
            Id = Guid.NewGuid(),
            StageId = stageId,
            Name = trimmed,
            Order = order.Count
        };

        await _connection.InsertAsync(newGroup);
        return MapToDomain(newGroup);
    }

    public async Task RenameGroupAsync(Guid stageId, Guid groupId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return;

        var entity = await _connection.Table<StageGroupEntity>()
            .FirstOrDefaultAsync(g => g.StageId == stageId && g.Id == groupId);
        if (entity is null)
            return;

        entity.Name = newName.Trim();
        await _connection.UpdateAsync(entity);
    }

    public async Task DeleteGroupAsync(Guid stageId, Guid groupId)
    {
        // Освобождаем команды, которые были в этой группе внутри стадии.
        await _connection.ExecuteAsync(
            "UPDATE StageTeams SET GroupId = NULL WHERE StageId = ? AND GroupId = ?",
            stageId,
            groupId);

        await _connection.DeleteAsync<StageGroupEntity>(groupId);
    }

    private static GroupInfo MapToDomain(StageGroupEntity e) =>
        new()
        {
            Id = e.Id,
            Name = e.Name,
            Order = e.Order
        };
}

