using SQLite;

namespace HockeyTournamentTracker.Data;

public sealed class LocalDatabase
{
    private readonly SQLiteAsyncConnection _connection;

    public LocalDatabase(string databasePath)
    {
        _connection = new SQLiteAsyncConnection(databasePath);
    }

    public async Task InitializeAsync()
    {
        await _connection.CreateTableAsync<LeagueEntity>();
        await _connection.CreateTableAsync<TournamentEntity>();
        await _connection.CreateTableAsync<TeamEntity>();
        await _connection.CreateTableAsync<StageEntity>();
        await _connection.CreateTableAsync<StageGroupEntity>();
        await _connection.CreateTableAsync<StageTeamEntity>();
        await _connection.CreateTableAsync<MatchEntity>();
        await _connection.CreateTableAsync<PlayoffSettingsEntity>();
        await _connection.CreateTableAsync<PlayoffRoundEntity>();
        await _connection.CreateTableAsync<PlayoffSeriesEntity>();
        await _connection.CreateTableAsync<StageColorZoneEntity>();
        await _connection.CreateTableAsync<StageTeamZoneEntity>();

        // Миграция: добавить колонку IconPath в Teams, если её ещё нет
        try { await _connection.ExecuteAsync("ALTER TABLE Teams ADD COLUMN IconPath TEXT"); }
        catch { /* колонка уже есть */ }

        // Миграция: добавить колонку GroupId в Teams
        try { await _connection.ExecuteAsync("ALTER TABLE Teams ADD COLUMN GroupId TEXT"); }
        catch { /* колонка уже есть */ }

        // Миграция: добавить колонку GroupId в StageTeams
        try { await _connection.ExecuteAsync("ALTER TABLE StageTeams ADD COLUMN GroupId TEXT"); }
        catch { /* колонка уже есть */ }

        // Миграция: добавить колонку PeriodScoresJson в Matches
        try { await _connection.ExecuteAsync("ALTER TABLE Matches ADD COLUMN PeriodScoresJson TEXT"); }
        catch { /* колонка уже есть */ }

        // Миграция: добавить колонку StageId в Matches
        try { await _connection.ExecuteAsync("ALTER TABLE Matches ADD COLUMN StageId TEXT"); }
        catch { /* колонка уже есть */ }

        // Миграция: добавить колонку SeriesId в Matches
        try { await _connection.ExecuteAsync("ALTER TABLE Matches ADD COLUMN SeriesId TEXT"); }
        catch { /* колонка уже есть */ }

        // Миграция: добавить колонку LeagueId в Tournaments
        try { await _connection.ExecuteAsync("ALTER TABLE Tournaments ADD COLUMN LeagueId TEXT"); }
        catch { /* колонка уже есть */ }

        // Миграция: добавить колонку Sport в Leagues
        try { await _connection.ExecuteAsync("ALTER TABLE Leagues ADD COLUMN Sport TEXT"); }
        catch { /* колонка уже есть */ }

        await _connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Tournaments_LeagueId ON Tournaments(LeagueId)");

        // Индексы для быстрых фильтров списка матчей по стадии/дате/серии.
        await _connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Matches_StageId ON Matches(StageId)");
        await _connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Matches_DateTime ON Matches(DateTime)");
        await _connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Matches_SeriesId ON Matches(SeriesId)");
    }

    public SQLiteAsyncConnection Connection => _connection;
}

