using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Sqlite;

public static class SqliteRepositoryGuards
{
    public static void AssertExistingIdBelongsToGroup(SqliteConnection connection, string table, string id, string groupId)
    {
        var sql = table switch
        {
            "participants" => "SELECT group_id FROM participants WHERE id = $id",
            "economic_units" => "SELECT group_id FROM economic_units WHERE id = $id",
            "expenses" => "SELECT group_id FROM expenses WHERE id = $id",
            "transfers" => "SELECT group_id FROM transfers WHERE id = $id",
            _ => throw new ArgumentOutOfRangeException(nameof(table), table, "Unknown table")
        };

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", id);
        var existingGroupId = command.ExecuteScalar() as string;
        if (!string.IsNullOrEmpty(existingGroupId) && !string.Equals(existingGroupId, groupId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Cannot reuse {table} id in another group: {id}");
        }
    }
}
