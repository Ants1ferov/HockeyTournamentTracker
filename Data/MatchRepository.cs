using System.Text.Json;
using HockeyTournamentTracker.Domain;
using SQLite;

namespace HockeyTournamentTracker.Data;

public interface IMatchRepository
{
    Task<IReadOnlyList<Match>> GetByTournamentAsync(Guid tournamentId);
    Task<StageMatchPageResult> GetByStageAsync(StageMatchQuery query);
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

    public async Task<StageMatchPageResult> GetByStageAsync(StageMatchQuery query)
    {
        var limit = query.Limit <= 0 ? 50 : query.Limit;
        var offset = query.Offset < 0 ? 0 : query.Offset;

        if (string.IsNullOrWhiteSpace(query.TeamSearch))
        {
            var q = _connection.Table<MatchEntity>()
                .Where(m => m.TournamentId == query.TournamentId && m.StageId == query.StageId);

            if (query.SeriesId.HasValue)
                q = q.Where(m => m.SeriesId == query.SeriesId.Value);
            if (query.Status.HasValue)
                q = q.Where(m => m.Status == (int)query.Status.Value);
            if (query.DateFrom.HasValue)
                q = q.Where(m => m.DateTime >= query.DateFrom.Value);
            if (query.DateTo.HasValue)
                q = q.Where(m => m.DateTime <= query.DateTo.Value);

            q = query.SortDescending
                ? q.OrderByDescending(m => m.DateTime)
                : q.OrderBy(m => m.DateTime);

            var pageEntities = await q.Skip(offset).Take(limit + 1).ToListAsync();
            var hasMore = pageEntities.Count > limit;
            var normalized = pageEntities.Take(limit).Select(MapToDomain).ToList();
            return new StageMatchPageResult(normalized, hasMore);
        }

        var pattern = $"%{query.TeamSearch.Trim().ToLowerInvariant()}%";
        var sql = """
                  SELECT m.*
                  FROM Matches m
                  LEFT JOIN Teams ht ON ht.Id = m.HomeTeamId
                  LEFT JOIN Teams at ON at.Id = m.AwayTeamId
                  WHERE m.TournamentId = ?
                    AND m.StageId = ?
                    AND (LOWER(IFNULL(ht.Name, '')) LIKE ? OR LOWER(IFNULL(at.Name, '')) LIKE ?)
                  """;
        var args = new List<object> { query.TournamentId, query.StageId, pattern, pattern };

        if (query.SeriesId.HasValue)
        {
            sql += " AND m.SeriesId = ?";
            args.Add(query.SeriesId.Value);
        }
        if (query.Status.HasValue)
        {
            sql += " AND m.Status = ?";
            args.Add((int)query.Status.Value);
        }
        if (query.DateFrom.HasValue)
        {
            sql += " AND m.DateTime >= ?";
            args.Add(query.DateFrom.Value);
        }
        if (query.DateTo.HasValue)
        {
            sql += " AND m.DateTime <= ?";
            args.Add(query.DateTo.Value);
        }

        sql += query.SortDescending
            ? " ORDER BY m.DateTime DESC, m.Id DESC"
            : " ORDER BY m.DateTime ASC, m.Id ASC";
        sql += " LIMIT ? OFFSET ?";
        args.Add(limit + 1);
        args.Add(offset);

        var searchedEntities = await _connection.QueryAsync<MatchEntity>(sql, args.ToArray());
        var searchedHasMore = searchedEntities.Count > limit;
        var searchedNormalized = searchedEntities.Take(limit).Select(MapToDomain).ToList();
        return new StageMatchPageResult(searchedNormalized, searchedHasMore);
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
            SeriesId = entity.SeriesId,
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
            SeriesId = match.SeriesId,
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

public sealed class StageMatchQuery
{
    public Guid TournamentId { get; set; }
    public Guid StageId { get; set; }
    public Guid? SeriesId { get; set; }
    public string? TeamSearch { get; set; }
    public MatchStatus? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; } = 50;
    public bool SortDescending { get; set; } = true;
}

public sealed class StageMatchPageResult
{
    public StageMatchPageResult(IReadOnlyList<Match> items, bool hasMore)
    {
        Items = items;
        HasMore = hasMore;
    }

    public IReadOnlyList<Match> Items { get; }
    public bool HasMore { get; }
}

