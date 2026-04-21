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

        ApplyV2(connection);
        ApplyV3(connection);

        return Task.CompletedTask;
    }

    private static readonly string[] MigrationV2Sql =
    [
        "ALTER TABLE groups ADD COLUMN is_shared INTEGER NOT NULL DEFAULT 0 CHECK (is_shared IN (0, 1))",
        "ALTER TABLE groups ADD COLUMN remote_container_name TEXT",
        "ALTER TABLE groups ADD COLUMN owner_id TEXT",
        "ALTER TABLE groups ADD COLUMN current_key_version INTEGER NOT NULL DEFAULT 0",
        "ALTER TABLE groups ADD COLUMN sync_status TEXT NOT NULL DEFAULT 'UpToDate'",
        "ALTER TABLE groups ADD COLUMN is_read_only INTEGER NOT NULL DEFAULT 0 CHECK (is_read_only IN (0, 1))",
        "CREATE TABLE IF NOT EXISTS group_memberships (group_id TEXT NOT NULL, user_id TEXT NOT NULL, role TEXT NOT NULL CHECK (role IN ('Owner', 'Member')), joined_at TEXT NOT NULL, is_revoked INTEGER NOT NULL DEFAULT 0 CHECK (is_revoked IN (0, 1)), revoked_at TEXT, PRIMARY KEY (group_id, user_id), FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE CASCADE)",
        "CREATE TABLE IF NOT EXISTS operations (operation_id TEXT PRIMARY KEY, group_id TEXT NOT NULL, device_id TEXT NOT NULL, user_id TEXT NOT NULL, hlc_timestamp TEXT NOT NULL, operation_type TEXT NOT NULL, entity_id TEXT NOT NULL, encrypted_payload BLOB NOT NULL, key_version INTEGER NOT NULL, created_at TEXT NOT NULL, FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE CASCADE)",
        "CREATE TABLE IF NOT EXISTS sync_cursors (device_id TEXT NOT NULL, group_id TEXT NOT NULL, last_synced_hlc_timestamp TEXT NOT NULL, last_synced_at TEXT NOT NULL, PRIMARY KEY (device_id, group_id))",
        "CREATE INDEX IF NOT EXISTS idx_operations_group_hlc ON operations(group_id, hlc_timestamp)",
        "CREATE INDEX IF NOT EXISTS idx_sync_cursors_group ON sync_cursors(group_id)"
    ];

    private static void ApplyV2(SqliteConnection connection)
    {
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT version FROM schema_version WHERE version = 2";
        var existing = query.ExecuteScalar();
        if (existing is long v && v == 2) return;

        using var begin = connection.CreateCommand();
        begin.CommandText = "BEGIN";
        begin.ExecuteNonQuery();

        try
        {
            foreach (var statement in MigrationV2Sql)
            {
                using var command = connection.CreateCommand();
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }

            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO schema_version (version, applied_at) VALUES (2, $appliedAt)";
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

    private static readonly string[] MigrationV3Sql =
    [
        @"CREATE TABLE IF NOT EXISTS activity_entries (
            entry_id TEXT PRIMARY KEY,
            group_id TEXT NOT NULL,
            entry_type TEXT NOT NULL,
            actor_user_id TEXT NOT NULL,
            entity_id TEXT,
            description TEXT,
            occurred_at TEXT NOT NULL,
            FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE CASCADE)",
        "CREATE INDEX IF NOT EXISTS idx_activity_entries_group_occurred ON activity_entries(group_id, occurred_at DESC)"
    ];

    private static void ApplyV3(SqliteConnection connection)
    {
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT version FROM schema_version WHERE version = 3";
        var existing = query.ExecuteScalar();
        if (existing is long v && v == 3) return;

        using var begin = connection.CreateCommand();
        begin.CommandText = "BEGIN";
        begin.ExecuteNonQuery();

        try
        {
            foreach (var statement in MigrationV3Sql)
            {
                using var command = connection.CreateCommand();
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }

            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO schema_version (version, applied_at) VALUES (3, $appliedAt)";
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

    private static void RepairOrphanedEconomicUnits(SqliteConnection connection)    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "DELETE FROM economic_units " +
            "WHERE id NOT IN (SELECT DISTINCT economic_unit_id FROM participants WHERE economic_unit_id IS NOT NULL)";
        command.ExecuteNonQuery();
    }
}
