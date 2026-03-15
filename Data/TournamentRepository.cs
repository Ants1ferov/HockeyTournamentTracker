using HockeyTournamentTracker.Domain;
using SQLite;
using System.Text.Json;

namespace HockeyTournamentTracker.Data;

public interface ITournamentRepository
{
    Task<IReadOnlyList<Tournament>> GetAllAsync();
    Task<Tournament?> GetByIdAsync(Guid id);
    Task SaveAsync(Tournament tournament);
    Task DeleteAsync(Guid id);
}

public sealed class TournamentRepository : ITournamentRepository
{
    private readonly SQLiteAsyncConnection _connection;

    public TournamentRepository(LocalDatabase database)
    {
        _connection = database.Connection;
    }

    public async Task<IReadOnlyList<Tournament>> GetAllAsync()
    {
        var entities = await _connection.Table<TournamentEntity>().ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<Tournament?> GetByIdAsync(Guid id)
    {
        var entity = await _connection.FindAsync<TournamentEntity>(id);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task SaveAsync(Tournament tournament)
    {
        var entity = MapToEntity(tournament);
        if (await _connection.FindAsync<TournamentEntity>(tournament.Id) is null)
        {
            await _connection.InsertAsync(entity);
        }
        else
        {
            await _connection.UpdateAsync(entity);
        }
    }

    public Task DeleteAsync(Guid id)
    {
        return _connection.DeleteAsync<TournamentEntity>(id);
    }

    private static Tournament MapToDomain(TournamentEntity entity)
    {
        var rules = entity.RulesJson is { Length: > 0 }
            ? JsonSerializer.Deserialize<TournamentRules>(entity.RulesJson) ?? new TournamentRules()
            : new TournamentRules();

        return new Tournament
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Status = (TournamentStatus)entity.Status,
            Rules = rules
        };
    }

    private static TournamentEntity MapToEntity(Tournament tournament)
    {
        return new TournamentEntity
        {
            Id = tournament.Id == Guid.Empty ? Guid.NewGuid() : tournament.Id,
            Name = tournament.Name,
            Description = tournament.Description,
            StartDate = tournament.StartDate,
            EndDate = tournament.EndDate,
            Status = (int)tournament.Status,
            RulesJson = JsonSerializer.Serialize(tournament.Rules)
        };
    }
}

