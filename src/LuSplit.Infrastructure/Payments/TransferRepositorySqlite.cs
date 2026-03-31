using LuSplit.Application.Payments.Ports;
using LuSplit.Domain.Payments;
using LuSplit.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace LuSplit.Infrastructure.Payments;

public sealed class TransferRepositorySqlite : ITransferRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public TransferRepositorySqlite(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task<IReadOnlyList<Transfer>> ListTransfersByGroupIdAsync(string groupId, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"SELECT id, group_id, from_participant_id, to_participant_id, amount_minor, date, type, note
FROM transfers
WHERE group_id = $groupId
ORDER BY id";
        command.Parameters.AddWithValue("$groupId", groupId);

        var transfers = new List<Transfer>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            transfers.Add(new Transfer(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt64(4),
                reader.GetString(5),
                SqliteEnumConverters.ParseTransferType(reader.GetString(6)),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return Task.FromResult<IReadOnlyList<Transfer>>(transfers);
    }

    public Task SaveTransferAsync(Transfer transfer, CancellationToken cancellationToken)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            SqliteRepositoryGuards.AssertExistingIdBelongsToGroup(_connection, "transfers", transfer.Id, transfer.GroupId);

            using var command = _connection.CreateCommand();
            command.CommandText = @"INSERT INTO transfers (
  group_id, id, from_participant_id, to_participant_id, amount_minor, date, type, note
) VALUES ($groupId, $id, $fromId, $toId, $amountMinor, $date, $type, $note)
ON CONFLICT(id) DO UPDATE SET
  from_participant_id = excluded.from_participant_id,
  to_participant_id = excluded.to_participant_id,
  amount_minor = excluded.amount_minor,
  date = excluded.date,
  type = excluded.type,
  note = excluded.note";
            command.Parameters.AddWithValue("$groupId", transfer.GroupId);
            command.Parameters.AddWithValue("$id", transfer.Id);
            command.Parameters.AddWithValue("$fromId", transfer.FromParticipantId);
            command.Parameters.AddWithValue("$toId", transfer.ToParticipantId);
            command.Parameters.AddWithValue("$amountMinor", transfer.AmountMinor);
            command.Parameters.AddWithValue("$date", transfer.Date);
            command.Parameters.AddWithValue("$type", SqliteEnumConverters.ToTransferTypeString(transfer.Type));
            command.Parameters.AddWithValue("$note", (object?)transfer.Note ?? DBNull.Value);
            command.ExecuteNonQuery();
            await Task.CompletedTask;
        });
}
