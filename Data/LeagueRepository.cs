using HockeyTournamentTracker.Domain;
using SQLite;

namespace HockeyTournamentTracker.Data;

public interface ILeagueRepository
{
    Task<IReadOnlyList<League>> GetAllAsync();
    Task<League?> GetByIdAsync(Guid id);
    Task SaveAsync(League league);
    Task DeleteAsync(Guid id);
}

public sealed class LeagueRepository : ILeagueRepository
{
    private readonly SQLiteAsyncConnection _connection;

    public LeagueRepository(LocalDatabase database)
    {
        _connection = database.Connection;
    }

    public async Task<IReadOnlyList<League>> GetAllAsync()
    {
        var entities = await _connection.Table<LeagueEntity>().OrderBy(e => e.Order).ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<League?> GetByIdAsync(Guid id)
    {
        var entity = await _connection.FindAsync<LeagueEntity>(id);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task SaveAsync(League league)
    {
        var entity = MapToEntity(league);
        if (await _connection.FindAsync<LeagueEntity>(entity.Id) is null)
            await _connection.InsertAsync(entity);
        else
            await _connection.UpdateAsync(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _connection.ExecuteAsync("UPDATE Tournaments SET LeagueId = NULL WHERE LeagueId = ?", id.ToString());
        await _connection.DeleteAsync<LeagueEntity>(id);
    }

    private static League MapToDomain(LeagueEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Description = e.Description,
        IconPath = e.IconPath,
        Order = e.Order
    };

    private static LeagueEntity MapToEntity(League l) => new()
    {
        Id = l.Id == Guid.Empty ? Guid.NewGuid() : l.Id,
        Name = l.Name,
        Description = l.Description,
        IconPath = l.IconPath,
        Order = l.Order
    };
}
