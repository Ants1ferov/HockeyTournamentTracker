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
        await _connection.CreateTableAsync<TournamentEntity>();
        await _connection.CreateTableAsync<TeamEntity>();
        await _connection.CreateTableAsync<StageEntity>();
        await _connection.CreateTableAsync<MatchEntity>();

        // Миграция: добавить колонку IconPath в Teams, если её ещё нет
        try { await _connection.ExecuteAsync("ALTER TABLE Teams ADD COLUMN IconPath TEXT"); }
        catch { /* колонка уже есть */ }

        // Миграция: добавить колонку GroupId в Teams
        try { await _connection.ExecuteAsync("ALTER TABLE Teams ADD COLUMN GroupId TEXT"); }
        catch { /* колонка уже есть */ }

        // Миграция: добавить колонку PeriodScoresJson в Matches
        try { await _connection.ExecuteAsync("ALTER TABLE Matches ADD COLUMN PeriodScoresJson TEXT"); }
        catch { /* колонка уже есть */ }

        // Миграция: добавить колонку StageId в Matches
        try { await _connection.ExecuteAsync("ALTER TABLE Matches ADD COLUMN StageId TEXT"); }
        catch { /* колонка уже есть */ }
    }

    public SQLiteAsyncConnection Connection => _connection;
}

