using HockeyTournamentTracker.Domain;
using SQLite;

namespace HockeyTournamentTracker.Data;

public interface IPlayoffRepository
{
    Task<PlayoffSettings> GetSettingsAsync(Guid stageId);
    Task SaveSettingsAsync(PlayoffSettings settings);

    Task<IReadOnlyList<PlayoffRound>> GetRoundsAsync(Guid stageId);
    Task SaveRoundAsync(PlayoffRound round);
    Task DeleteRoundAsync(Guid roundId);

    Task<IReadOnlyList<PlayoffSeries>> GetSeriesByRoundAsync(Guid roundId);
    Task<IReadOnlyList<PlayoffSeries>> GetSeriesByStageAsync(Guid stageId);
    Task SaveSeriesAsync(PlayoffSeries series);
    Task DeleteSeriesAsync(Guid seriesId);
}

public sealed class PlayoffRepository : IPlayoffRepository
{
    private readonly SQLiteAsyncConnection _connection;

    public PlayoffRepository(LocalDatabase database)
    {
        _connection = database.Connection;
    }

    public async Task<PlayoffSettings> GetSettingsAsync(Guid stageId)
    {
        var entity = await _connection.FindAsync<PlayoffSettingsEntity>(stageId);
        if (entity is null)
            return new PlayoffSettings { StageId = stageId };

        return new PlayoffSettings
        {
            StageId = entity.StageId,
            UseReseeding = entity.UseReseeding == 1,
            HasThirdPlaceMatch = entity.HasThirdPlaceMatch == 1
        };
    }

    public async Task SaveSettingsAsync(PlayoffSettings settings)
    {
        var entity = new PlayoffSettingsEntity
        {
            StageId = settings.StageId,
            UseReseeding = settings.UseReseeding ? 1 : 0,
            HasThirdPlaceMatch = settings.HasThirdPlaceMatch ? 1 : 0
        };

        if (await _connection.FindAsync<PlayoffSettingsEntity>(settings.StageId) is null)
            await _connection.InsertAsync(entity);
        else
            await _connection.UpdateAsync(entity);
    }

    public async Task<IReadOnlyList<PlayoffRound>> GetRoundsAsync(Guid stageId)
    {
        var entities = await _connection.Table<PlayoffRoundEntity>()
            .Where(r => r.StageId == stageId)
            .ToListAsync();

        return entities
            .OrderBy(r => r.Order)
            .ThenBy(r => r.Name)
            .Select(MapRound)
            .ToList();
    }

    public async Task SaveRoundAsync(PlayoffRound round)
    {
        var entity = new PlayoffRoundEntity
        {
            Id = round.Id == Guid.Empty ? Guid.NewGuid() : round.Id,
            StageId = round.StageId,
            Name = round.Name,
            Order = round.Order,
            DefaultBestOf = round.DefaultBestOf
        };

        if (await _connection.FindAsync<PlayoffRoundEntity>(entity.Id) is null)
            await _connection.InsertAsync(entity);
        else
            await _connection.UpdateAsync(entity);
    }

    public async Task DeleteRoundAsync(Guid roundId)
    {
        await _connection.Table<PlayoffSeriesEntity>()
            .Where(s => s.RoundId == roundId)
            .DeleteAsync();
        await _connection.DeleteAsync<PlayoffRoundEntity>(roundId);
    }

    public async Task<IReadOnlyList<PlayoffSeries>> GetSeriesByRoundAsync(Guid roundId)
    {
        var entities = await _connection.Table<PlayoffSeriesEntity>()
            .Where(s => s.RoundId == roundId)
            .ToListAsync();

        return entities
            .OrderBy(s => s.Slot)
            .Select(MapSeries)
            .ToList();
    }

    public async Task<IReadOnlyList<PlayoffSeries>> GetSeriesByStageAsync(Guid stageId)
    {
        var entities = await _connection.Table<PlayoffSeriesEntity>()
            .Where(s => s.StageId == stageId)
            .ToListAsync();

        return entities
            .OrderBy(s => s.RoundId)
            .ThenBy(s => s.Slot)
            .Select(MapSeries)
            .ToList();
    }

    public async Task SaveSeriesAsync(PlayoffSeries series)
    {
        var entity = new PlayoffSeriesEntity
        {
            Id = series.Id == Guid.Empty ? Guid.NewGuid() : series.Id,
            StageId = series.StageId,
            RoundId = series.RoundId,
            Slot = series.Slot,
            HomeTeamId = series.HomeTeamId,
            AwayTeamId = series.AwayTeamId,
            HomeSeed = series.HomeSeed,
            AwaySeed = series.AwaySeed,
            BestOfOverride = series.BestOfOverride,
            WinnerTeamId = series.WinnerTeamId,
            IsThirdPlace = series.IsThirdPlace ? 1 : 0
        };

        if (await _connection.FindAsync<PlayoffSeriesEntity>(entity.Id) is null)
            await _connection.InsertAsync(entity);
        else
            await _connection.UpdateAsync(entity);
    }

    public Task DeleteSeriesAsync(Guid seriesId) =>
        _connection.DeleteAsync<PlayoffSeriesEntity>(seriesId);

    private static PlayoffRound MapRound(PlayoffRoundEntity e) =>
        new()
        {
            Id = e.Id,
            StageId = e.StageId,
            Name = e.Name,
            Order = e.Order,
            DefaultBestOf = e.DefaultBestOf <= 0 ? 1 : e.DefaultBestOf
        };

    private static PlayoffSeries MapSeries(PlayoffSeriesEntity e) =>
        new()
        {
            Id = e.Id,
            StageId = e.StageId,
            RoundId = e.RoundId,
            Slot = e.Slot,
            HomeTeamId = e.HomeTeamId,
            AwayTeamId = e.AwayTeamId,
            HomeSeed = e.HomeSeed,
            AwaySeed = e.AwaySeed,
            BestOfOverride = e.BestOfOverride,
            WinnerTeamId = e.WinnerTeamId,
            IsThirdPlace = e.IsThirdPlace == 1
        };
}
