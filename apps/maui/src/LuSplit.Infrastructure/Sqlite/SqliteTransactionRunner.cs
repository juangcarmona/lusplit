using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Sqlite;

public sealed class SqliteTransactionRunner
{
    private readonly SqliteConnection _connection;
    private int _depth;

    public SqliteTransactionRunner(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task<T> RunInTransactionAsync<T>(Func<Task<T>> action)
    {
        _depth += 1;
        var isOutermost = _depth == 1;

        if (isOutermost)
        {
            using var begin = _connection.CreateCommand();
            begin.CommandText = "BEGIN";
            begin.ExecuteNonQuery();
        }

        try
        {
            var result = await action();

            if (isOutermost)
            {
                using var commit = _connection.CreateCommand();
                commit.CommandText = "COMMIT";
                commit.ExecuteNonQuery();
            }

            return result;
        }
        catch
        {
            if (isOutermost)
            {
                using var rollback = _connection.CreateCommand();
                rollback.CommandText = "ROLLBACK";
                rollback.ExecuteNonQuery();
            }

            throw;
        }
        finally
        {
            _depth -= 1;
        }
    }

    public Task RunInTransactionAsync(Func<Task> action)
        => RunInTransactionAsync(async () =>
        {
            await action();
            return true;
        });
}
