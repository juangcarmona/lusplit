using LuSplit.Infrastructure.Activity;
using LuSplit.Infrastructure.Expenses;
using LuSplit.Infrastructure.Groups;
using LuSplit.Infrastructure.Payments;
using LuSplit.Infrastructure.Snapshot;
using LuSplit.Infrastructure.Sync;
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

    public SharedGroupStateRepositorySqlite SharedGroupStateRepository { get; }

    public OperationRepositorySqlite OperationRepository { get; }

    public SyncCursorRepositorySqlite SyncCursorRepository { get; }

    public ActivityEntryRepository ActivityEntryRepository { get; }

    public GroupMembershipRepositorySqlite GroupMembershipRepository { get; }

    private InfraLocalSqlite(SqliteConnection db)
    {
        Db = db;
        _transactionRunner = new SqliteTransactionRunner(Db);
        GroupRepository = new GroupRepositorySqlite(Db, _transactionRunner);
        ParticipantRepository = new ParticipantRepositorySqlite(Db, _transactionRunner);
        EconomicUnitRepository = new EconomicUnitRepositorySqlite(Db, _transactionRunner);
        ExpenseRepository = new ExpenseRepositorySqlite(Db, _transactionRunner);
        TransferRepository = new TransferRepositorySqlite(Db, _transactionRunner);
        SharedGroupStateRepository = new SharedGroupStateRepositorySqlite(Db, _transactionRunner);
        OperationRepository = new OperationRepositorySqlite(Db, _transactionRunner);
        SyncCursorRepository = new SyncCursorRepositorySqlite(Db, _transactionRunner);
        ActivityEntryRepository = new ActivityEntryRepository(Db, _transactionRunner);
        GroupMembershipRepository = new GroupMembershipRepositorySqlite(Db, _transactionRunner);
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
