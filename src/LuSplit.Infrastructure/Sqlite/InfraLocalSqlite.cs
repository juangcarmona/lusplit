using LuSplit.Infrastructure.Expenses;
using LuSplit.Infrastructure.Groups;
using LuSplit.Infrastructure.Payments;
using LuSplit.Infrastructure.Snapshot;
using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Sqlite;

public sealed class InfraLocalSqlite : IDisposable
{
    private readonly SqliteTransactionRunner _transactionRunner;

    public SqliteConnection Db { get; }

    public GroupRepositorySqlite GroupRepository { get; }

    public ParticipantRepositorySqlite ParticipantRepository { get; }

    public EconomicUnitRepositorySqlite EconomicUnitRepository { get; }

    public ExpenseRepositorySqlite ExpenseRepository { get; }

    public TransferRepositorySqlite TransferRepository { get; }

    private InfraLocalSqlite(SqliteConnection db)
    {
        Db = db;
        _transactionRunner = new SqliteTransactionRunner(Db);
        GroupRepository = new GroupRepositorySqlite(Db, _transactionRunner);
        ParticipantRepository = new ParticipantRepositorySqlite(Db, _transactionRunner);
        EconomicUnitRepository = new EconomicUnitRepositorySqlite(Db, _transactionRunner);
        ExpenseRepository = new ExpenseRepositorySqlite(Db, _transactionRunner);
        TransferRepository = new TransferRepositorySqlite(Db, _transactionRunner);
    }

    public static async Task<InfraLocalSqlite> CreateAsync(string? databasePath = null)
    {
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath ?? ":memory:",
            ForeignKeys = true,
            Pooling = false
        };

        var connection = new SqliteConnection(csb.ConnectionString);
        connection.Open();
        await SqliteMigrations.ApplyAsync(connection);
        return new InfraLocalSqlite(connection);
    }

    public Task<T> RunInTransactionAsync<T>(Func<Task<T>> action)
        => _transactionRunner.RunInTransactionAsync(action);

    public Task RunInTransactionAsync(Func<Task> action)
        => _transactionRunner.RunInTransactionAsync(action);

    public Task<GroupSnapshotV1> ExportGroupSnapshotAsync(string groupId)
        => SnapshotService.ExportGroupSnapshotAsync(Db, groupId);

    public Task ImportGroupSnapshotAsync(object snapshot)
        => SnapshotService.ImportGroupSnapshotAsync(Db, _transactionRunner, snapshot);

    public void Dispose()
    {
        Db.Dispose();
    }
}
