using System.Linq;
using HockeyTournamentTracker.Domain;
using SQLite;

namespace HockeyTournamentTracker.Data;

public interface IStageRepository
{
    Task<IReadOnlyList<Stage>> GetByTournamentAsync(Guid tournamentId);
    Task<Stage?> GetByIdAsync(Guid id);
    Task SaveAsync(Stage stage);
    Task DeleteAsync(Guid id);
}

public sealed class StageRepository : IStageRepository
{
    private readonly SQLiteAsyncConnection _connection;

    public StageRepository(LocalDatabase database)
    {
        _connection = database.Connection;
    }

    public async Task<IReadOnlyList<Stage>> GetByTournamentAsync(Guid tournamentId)
    {
        var entities = await _connection.Table<StageEntity>()
            .Where(s => s.TournamentId == tournamentId)
            .ToListAsync();
        return entities.OrderBy(s => s.Order).ThenBy(s => s.Name).Select(MapToDomain).ToList();
    }

    public async Task<Stage?> GetByIdAsync(Guid id)
    {
        var entity = await _connection.FindAsync<StageEntity>(id);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task SaveAsync(Stage stage)
    {
        var entity = MapToEntity(stage);
        if (await _connection.FindAsync<StageEntity>(entity.Id) is null)
            await _connection.InsertAsync(entity);
        else
            await _connection.UpdateAsync(entity);
    }

    public Task DeleteAsync(Guid id) => _connection.DeleteAsync<StageEntity>(id);

    private static Stage MapToDomain(StageEntity entity) =>
        new()
        {
            Id = entity.Id,
            TournamentId = entity.TournamentId,
            Name = entity.Name,
            Order = entity.Order,
            StageType = (StageType)entity.StageType
        };

    private static StageEntity MapToEntity(Stage stage) =>
        new()
        {
            Id = stage.Id == Guid.Empty ? Guid.NewGuid() : stage.Id,
            TournamentId = stage.TournamentId,
            Name = stage.Name,
            Order = stage.Order,
            StageType = (int)stage.StageType
        };
}
