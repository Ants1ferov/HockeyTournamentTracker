using HockeyTournamentTracker.Domain;
using SQLite;

namespace HockeyTournamentTracker.Data;

public interface ITeamRepository
{
    Task<IReadOnlyList<Team>> GetByTournamentAsync(Guid tournamentId);
    Task<Team?> GetByIdAsync(Guid id);
    Task SaveAsync(Team team);
    Task DeleteAsync(Guid id);
}

public sealed class TeamRepository : ITeamRepository
{
    private readonly SQLiteAsyncConnection _connection;

    public TeamRepository(LocalDatabase database)
    {
        _connection = database.Connection;
    }

    public async Task<IReadOnlyList<Team>> GetByTournamentAsync(Guid tournamentId)
    {
        var entities = await _connection.Table<TeamEntity>()
            .Where(t => t.TournamentId == tournamentId)
            .ToListAsync();

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<Team?> GetByIdAsync(Guid id)
    {
        var entity = await _connection.FindAsync<TeamEntity>(id);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task SaveAsync(Team team)
    {
        var entity = MapToEntity(team);

        if (await _connection.FindAsync<TeamEntity>(entity.Id) is null)
        {
            await _connection.InsertAsync(entity);
        }
        else
        {
            await _connection.UpdateAsync(entity);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var team = await GetByIdAsync(id);
        await _connection.DeleteAsync<TeamEntity>(id);
        if (team?.IconPath is { } path && File.Exists(path))
        {
            try { File.Delete(path); } catch { /* игнорируем */ }
        }
    }

    private static Team MapToDomain(TeamEntity entity) =>
        new()
        {
            Id = entity.Id,
            TournamentId = entity.TournamentId,
            Name = entity.Name,
            ShortName = entity.ShortName,
            ColorHex = entity.ColorHex,
            IconPath = entity.IconPath,
            Notes = entity.Notes,
            GroupId = entity.GroupId
        };

    private static TeamEntity MapToEntity(Team team) =>
        new()
        {
            Id = team.Id == Guid.Empty ? Guid.NewGuid() : team.Id,
            TournamentId = team.TournamentId,
            Name = team.Name,
            ShortName = team.ShortName,
            ColorHex = team.ColorHex,
            IconPath = team.IconPath,
            Notes = team.Notes,
            GroupId = team.GroupId
        };
}

