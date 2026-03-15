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
    }

    public SQLiteAsyncConnection Connection => _connection;
}

