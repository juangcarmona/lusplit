using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Sqlite;

public static class SqliteMigrations
{
    private static readonly string[] MigrationV1Sql =
    [
        "CREATE TABLE IF NOT EXISTS groups (id TEXT PRIMARY KEY, currency TEXT NOT NULL, closed INTEGER NOT NULL CHECK (closed IN (0, 1)))",
        "CREATE TABLE IF NOT EXISTS economic_units (group_id TEXT NOT NULL, id TEXT NOT NULL, owner_participant_id TEXT NOT NULL, name TEXT, PRIMARY KEY (group_id, id), UNIQUE (id), FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE NO ACTION)",
        "CREATE TABLE IF NOT EXISTS participants (group_id TEXT NOT NULL, id TEXT NOT NULL, economic_unit_id TEXT NOT NULL, name TEXT NOT NULL, consumption_category TEXT NOT NULL CHECK (consumption_category IN ('FULL', 'HALF', 'CUSTOM')), custom_consumption_weight TEXT, PRIMARY KEY (group_id, id), UNIQUE (id), FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE NO ACTION, FOREIGN KEY (group_id, economic_unit_id) REFERENCES economic_units(group_id, id) ON DELETE NO ACTION)",
        "CREATE TABLE IF NOT EXISTS expenses (group_id TEXT NOT NULL, id TEXT NOT NULL, title TEXT NOT NULL, paid_by_participant_id TEXT NOT NULL, amount_minor INTEGER NOT NULL, date TEXT NOT NULL, split_definition_json TEXT NOT NULL, notes TEXT, PRIMARY KEY (group_id, id), UNIQUE (id), FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE NO ACTION, FOREIGN KEY (group_id, paid_by_participant_id) REFERENCES participants(group_id, id) ON DELETE NO ACTION)",
        "CREATE TABLE IF NOT EXISTS transfers (group_id TEXT NOT NULL, id TEXT NOT NULL, from_participant_id TEXT NOT NULL, to_participant_id TEXT NOT NULL, amount_minor INTEGER NOT NULL, date TEXT NOT NULL, type TEXT NOT NULL CHECK (type IN ('GENERATED', 'MANUAL')), note TEXT, PRIMARY KEY (group_id, id), UNIQUE (id), FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE NO ACTION, FOREIGN KEY (group_id, from_participant_id) REFERENCES participants(group_id, id) ON DELETE NO ACTION, FOREIGN KEY (group_id, to_participant_id) REFERENCES participants(group_id, id) ON DELETE NO ACTION)",
        "CREATE TABLE IF NOT EXISTS projection_snapshots (id TEXT PRIMARY KEY, group_id TEXT NOT NULL, projection_type TEXT NOT NULL, payload_json TEXT NOT NULL, created_at TEXT NOT NULL, FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE NO ACTION)",
        "CREATE INDEX IF NOT EXISTS idx_participants_group_id ON participants(group_id, id)",
        "CREATE INDEX IF NOT EXISTS idx_economic_units_group_id ON economic_units(group_id, id)",
        "CREATE INDEX IF NOT EXISTS idx_expenses_group_id ON expenses(group_id, id)",
        "CREATE INDEX IF NOT EXISTS idx_transfers_group_id ON transfers(group_id, id)"
    ];

    public static Task ApplyAsync(SqliteConnection connection)
    {
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON";
            pragma.ExecuteNonQuery();
        }

        using (var schema = connection.CreateCommand())
        {
            schema.CommandText = "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER PRIMARY KEY, applied_at TEXT NOT NULL)";
            schema.ExecuteNonQuery();
        }

        using var query = connection.CreateCommand();
        query.CommandText = "SELECT version FROM schema_version WHERE version = 1";
        var existing = query.ExecuteScalar();
        if (existing is not long || (long)existing != 1)
        {
            using var begin = connection.CreateCommand();
            begin.CommandText = "BEGIN";
            begin.ExecuteNonQuery();

            try
            {
                foreach (var statement in MigrationV1Sql)
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = statement;
                    command.ExecuteNonQuery();
                }

                using var insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO schema_version (version, applied_at) VALUES (1, $appliedAt)";
                insert.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O"));
                insert.ExecuteNonQuery();

                using var commit = connection.CreateCommand();
                commit.CommandText = "COMMIT";
                commit.ExecuteNonQuery();
            }
            catch
            {
                using var rollback = connection.CreateCommand();
                rollback.CommandText = "ROLLBACK";
                rollback.ExecuteNonQuery();
                throw;
            }
        }

        // Always-run idempotent repair: remove orphaned economic units (units whose
        // owner participant was moved to a different unit by a previous bug, leaving
        // the unit with no members). Without this, BalanceCalculator throws a
        // DomainInvariantException at startup if a group has such stale data.
        RepairOrphanedEconomicUnits(connection);

        return Task.CompletedTask;
    }

    private static void RepairOrphanedEconomicUnits(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "DELETE FROM economic_units " +
            "WHERE id NOT IN (SELECT DISTINCT economic_unit_id FROM participants WHERE economic_unit_id IS NOT NULL)";
        command.ExecuteNonQuery();
    }
}
