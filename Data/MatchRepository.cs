using System.Text.Json;
using HockeyTournamentTracker.Domain;
using SQLite;

namespace HockeyTournamentTracker.Data;

public interface IMatchRepository
{
    Task<IReadOnlyList<Match>> GetByTournamentAsync(Guid tournamentId);
    Task<Match?> GetByIdAsync(Guid id);
    Task SaveAsync(Match match);
    Task DeleteAsync(Guid id);
}

public sealed class MatchRepository : IMatchRepository
{
    private readonly SQLiteAsyncConnection _connection;

    public MatchRepository(LocalDatabase database)
    {
        _connection = database.Connection;
    }

    public async Task<IReadOnlyList<Match>> GetByTournamentAsync(Guid tournamentId)
    {
        var entities = await _connection.Table<MatchEntity>()
            .Where(m => m.TournamentId == tournamentId)
            .ToListAsync();

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<Match?> GetByIdAsync(Guid id)
    {
        var entity = await _connection.FindAsync<MatchEntity>(id);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task SaveAsync(Match match)
    {
        var entity = MapToEntity(match);

        if (await _connection.FindAsync<MatchEntity>(entity.Id) is null)
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
        return _connection.DeleteAsync<MatchEntity>(id);
    }

    private static Match MapToDomain(MatchEntity entity)
    {
        var periodScores = entity.PeriodScoresJson is { Length: > 0 } json
            ? JsonSerializer.Deserialize<List<PeriodScore>>(json) ?? new List<PeriodScore>()
            : new List<PeriodScore>();
        return new Match
        {
            Id = entity.Id,
            TournamentId = entity.TournamentId,
            StageId = entity.StageId,
            DateTime = entity.DateTime,
            HomeTeamId = entity.HomeTeamId,
            AwayTeamId = entity.AwayTeamId,
            HomeGoals = entity.HomeGoals,
            AwayGoals = entity.AwayGoals,
            OutcomeType = entity.OutcomeType is null ? null : (OutcomeType?)entity.OutcomeType.Value,
            WinnerTeamId = entity.WinnerTeamId,
            LoserTeamId = entity.LoserTeamId,
            ShootoutScoreHome = entity.ShootoutScoreHome,
            ShootoutScoreAway = entity.ShootoutScoreAway,
            Status = (MatchStatus)entity.Status,
            Notes = entity.Notes,
            PeriodScores = periodScores
        };
    }

    private static MatchEntity MapToEntity(Match match) =>
        new()
        {
            Id = match.Id == Guid.Empty ? Guid.NewGuid() : match.Id,
            TournamentId = match.TournamentId,
            StageId = match.StageId,
            DateTime = match.DateTime,
            HomeTeamId = match.HomeTeamId,
            AwayTeamId = match.AwayTeamId,
            HomeGoals = match.HomeGoals,
            AwayGoals = match.AwayGoals,
            OutcomeType = match.OutcomeType is null ? null : (int?)match.OutcomeType.Value,
            WinnerTeamId = match.WinnerTeamId,
            LoserTeamId = match.LoserTeamId,
            ShootoutScoreHome = match.ShootoutScoreHome,
            ShootoutScoreAway = match.ShootoutScoreAway,
            Status = (int)match.Status,
            Notes = match.Notes,
            PeriodScoresJson = match.PeriodScores is { Count: > 0 } list
                ? JsonSerializer.Serialize(list)
                : null
        };
}

