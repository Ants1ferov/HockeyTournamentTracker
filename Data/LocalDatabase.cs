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
        await _connection.CreateTableAsync<MatchEntity>();

        // Миграция: добавить колонку IconPath в Teams, если её ещё нет
        try
        {
            await _connection.ExecuteAsync("ALTER TABLE Teams ADD COLUMN IconPath TEXT");
        }
        catch
        {
            // Колонка уже есть или таблица в старой схеме — игнорируем
        }
    }

    public SQLiteAsyncConnection Connection => _connection;
}

