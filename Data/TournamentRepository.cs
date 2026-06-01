using HockeyTournamentTracker.Domain;
using SQLite;
using System.Text.Json;

namespace HockeyTournamentTracker.Data;

public interface ITournamentRepository
{
    Task<IReadOnlyList<Tournament>> GetAllAsync();
    Task<Tournament?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<Tournament>> GetByLeagueAsync(Guid leagueId);
    Task SaveAsync(Tournament tournament);
    Task DeleteAsync(Guid id);
    Task SetLeagueAsync(Guid tournamentId, Guid? leagueId);
}

public sealed class TournamentRepository : ITournamentRepository
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly ITeamRepository _teamRepository;
    private readonly IMatchRepository _matchRepository;

    public TournamentRepository(LocalDatabase database, ITeamRepository teamRepository, IMatchRepository matchRepository)
    {
        _connection = database.Connection;
        _teamRepository = teamRepository;
        _matchRepository = matchRepository;
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

    public async Task<IReadOnlyList<Tournament>> GetByLeagueAsync(Guid leagueId)
    {
        var leagueIdStr = leagueId.ToString();
        var entities = await _connection.Table<TournamentEntity>()
            .Where(t => t.LeagueId == leagueIdStr)
            .ToListAsync();
        return entities.Select(MapToDomain).ToList();
    }

    public async Task SetLeagueAsync(Guid tournamentId, Guid? leagueId)
    {
        var leagueIdStr = leagueId.HasValue ? leagueId.Value.ToString() : null;
        await _connection.ExecuteAsync(
            "UPDATE Tournaments SET LeagueId = ? WHERE Id = ?",
            leagueIdStr, tournamentId.ToString());
    }

    public async Task DeleteAsync(Guid id)
    {
        var matches = await _matchRepository.GetByTournamentAsync(id);
        foreach (var m in matches)
            await _matchRepository.DeleteAsync(m.Id);

        var teams = await _teamRepository.GetByTournamentAsync(id);
        foreach (var t in teams)
            await _teamRepository.DeleteAsync(t.Id);

        await _connection.DeleteAsync<TournamentEntity>(id);
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
            Rules = rules,
            LeagueId = entity.LeagueId is { Length: > 0 } s && Guid.TryParse(s, out var lid) ? lid : null
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
            RulesJson = JsonSerializer.Serialize(tournament.Rules),
            LeagueId = tournament.LeagueId.HasValue ? tournament.LeagueId.Value.ToString() : null
        };
    }
}

