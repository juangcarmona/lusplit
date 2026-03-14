using System.Text.Json;
using LuSplit.Application.Commands;
using LuSplit.Application.Ports;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;
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

public sealed class GroupRepositorySqlite : IGroupRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public GroupRepositorySqlite(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task<Group?> GetByIdAsync(string groupId, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT id, currency, closed FROM groups WHERE id = $id";
        command.Parameters.AddWithValue("$id", groupId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return Task.FromResult<Group?>(null);
        }

        var group = new Group(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt64(2) == 1);

        return Task.FromResult<Group?>(group);
    }

    public Task SaveGroupAsync(Group group, CancellationToken cancellationToken)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"INSERT INTO groups (id, currency, closed)
VALUES ($id, $currency, $closed)
ON CONFLICT(id) DO UPDATE SET
  currency = excluded.currency,
  closed = excluded.closed";
            command.Parameters.AddWithValue("$id", group.Id);
            command.Parameters.AddWithValue("$currency", group.Currency);
            command.Parameters.AddWithValue("$closed", group.Closed ? 1 : 0);
            command.ExecuteNonQuery();
            await Task.CompletedTask;
        });
}

public sealed class ParticipantRepositorySqlite : IParticipantRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public ParticipantRepositorySqlite(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task<IReadOnlyList<Participant>> ListParticipantsByGroupIdAsync(string groupId, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"SELECT id, group_id, economic_unit_id, name, consumption_category, custom_consumption_weight
FROM participants
WHERE group_id = $groupId
ORDER BY id";
        command.Parameters.AddWithValue("$groupId", groupId);

        var participants = new List<Participant>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            participants.Add(new Participant(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                ParseConsumptionCategory(reader.GetString(4)),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return Task.FromResult<IReadOnlyList<Participant>>(participants);
    }

    public Task SaveParticipantAsync(Participant participant, CancellationToken cancellationToken)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            SqliteRepositoryGuards.AssertExistingIdBelongsToGroup(_connection, "participants", participant.Id, participant.GroupId);

            using var command = _connection.CreateCommand();
            command.CommandText = @"INSERT INTO participants (
  group_id, id, economic_unit_id, name, consumption_category, custom_consumption_weight
) VALUES ($groupId, $id, $economicUnitId, $name, $category, $weight)
ON CONFLICT(id) DO UPDATE SET
  economic_unit_id = excluded.economic_unit_id,
  name = excluded.name,
  consumption_category = excluded.consumption_category,
  custom_consumption_weight = excluded.custom_consumption_weight";
            command.Parameters.AddWithValue("$groupId", participant.GroupId);
            command.Parameters.AddWithValue("$id", participant.Id);
            command.Parameters.AddWithValue("$economicUnitId", participant.EconomicUnitId);
            command.Parameters.AddWithValue("$name", participant.Name);
            command.Parameters.AddWithValue("$category", ToConsumptionCategoryString(participant.ConsumptionCategory));
            command.Parameters.AddWithValue("$weight", (object?)participant.CustomConsumptionWeight ?? DBNull.Value);
            command.ExecuteNonQuery();
            await Task.CompletedTask;
        });

    private static ConsumptionCategory ParseConsumptionCategory(string value)
        => value switch
        {
            "FULL" => ConsumptionCategory.Full,
            "HALF" => ConsumptionCategory.Half,
            "CUSTOM" => ConsumptionCategory.Custom,
            _ => throw new InvalidOperationException($"Unknown consumption category: {value}")
        };

    private static string ToConsumptionCategoryString(ConsumptionCategory category)
        => category switch
        {
            ConsumptionCategory.Full => "FULL",
            ConsumptionCategory.Half => "HALF",
            ConsumptionCategory.Custom => "CUSTOM",
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown consumption category")
        };
}

public sealed class EconomicUnitRepositorySqlite : IEconomicUnitRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public EconomicUnitRepositorySqlite(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task<IReadOnlyList<EconomicUnit>> ListEconomicUnitsByGroupIdAsync(string groupId, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"SELECT id, group_id, owner_participant_id, name
FROM economic_units
WHERE group_id = $groupId
ORDER BY id";
        command.Parameters.AddWithValue("$groupId", groupId);

        var units = new List<EconomicUnit>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            units.Add(new EconomicUnit(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return Task.FromResult<IReadOnlyList<EconomicUnit>>(units);
    }

    public Task<EconomicUnit?> GetEconomicUnitByIdAsync(string economicUnitId, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT id, group_id, owner_participant_id, name FROM economic_units WHERE id = $id";
        command.Parameters.AddWithValue("$id", economicUnitId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return Task.FromResult<EconomicUnit?>(null);
        }

        return Task.FromResult<EconomicUnit?>(new EconomicUnit(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3)));
    }

    public Task SaveEconomicUnitAsync(EconomicUnit economicUnit, CancellationToken cancellationToken)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            SqliteRepositoryGuards.AssertExistingIdBelongsToGroup(_connection, "economic_units", economicUnit.Id, economicUnit.GroupId);

            using var command = _connection.CreateCommand();
            command.CommandText = @"INSERT INTO economic_units (group_id, id, owner_participant_id, name)
VALUES ($groupId, $id, $ownerParticipantId, $name)
ON CONFLICT(id) DO UPDATE SET
  owner_participant_id = excluded.owner_participant_id,
  name = excluded.name";
            command.Parameters.AddWithValue("$groupId", economicUnit.GroupId);
            command.Parameters.AddWithValue("$id", economicUnit.Id);
            command.Parameters.AddWithValue("$ownerParticipantId", economicUnit.OwnerParticipantId);
            command.Parameters.AddWithValue("$name", (object?)economicUnit.Name ?? DBNull.Value);
            command.ExecuteNonQuery();
            await Task.CompletedTask;
        });
}

public sealed class ExpenseRepositorySqlite : IExpenseRepository
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransactionRunner _transactionRunner;

    public ExpenseRepositorySqlite(SqliteConnection connection, SqliteTransactionRunner transactionRunner)
    {
        _connection = connection;
        _transactionRunner = transactionRunner;
    }

    public Task AddAsync(AddExpenseCommand command, CancellationToken cancellationToken)
        => throw new NotSupportedException("AddAsync(AddExpenseCommand) is not used in the MAUI application layer. Use SaveAsync(Expense).");

    public Task<IReadOnlyList<Expense>> ListExpensesByGroupIdAsync(string groupId, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"SELECT id, group_id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
FROM expenses
WHERE group_id = $groupId
ORDER BY id";
        command.Parameters.AddWithValue("$groupId", groupId);

        var expenses = new List<Expense>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            expenses.Add(new Expense(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt64(4),
                reader.GetString(5),
                SplitJson.ParseDefinition(reader.GetString(6)),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return Task.FromResult<IReadOnlyList<Expense>>(expenses);
    }

    public Task<Expense?> GetExpenseByIdAsync(string expenseId, CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"SELECT id, group_id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
FROM expenses
WHERE id = $id";
        command.Parameters.AddWithValue("$id", expenseId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return Task.FromResult<Expense?>(null);
        }

        return Task.FromResult<Expense?>(new Expense(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt64(4),
            reader.GetString(5),
            SplitJson.ParseDefinition(reader.GetString(6)),
            reader.IsDBNull(7) ? null : reader.GetString(7)));
    }

    public Task SaveAsync(Expense expense, CancellationToken cancellationToken)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            SqliteRepositoryGuards.AssertExistingIdBelongsToGroup(_connection, "expenses", expense.Id, expense.GroupId);

            using var command = _connection.CreateCommand();
            command.CommandText = @"INSERT INTO expenses (
  group_id, id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
) VALUES ($groupId, $id, $title, $paidBy, $amountMinor, $date, $split, $notes)
ON CONFLICT(id) DO UPDATE SET
  title = excluded.title,
  paid_by_participant_id = excluded.paid_by_participant_id,
  amount_minor = excluded.amount_minor,
  date = excluded.date,
  split_definition_json = excluded.split_definition_json,
  notes = excluded.notes";
            command.Parameters.AddWithValue("$groupId", expense.GroupId);
            command.Parameters.AddWithValue("$id", expense.Id);
            command.Parameters.AddWithValue("$title", expense.Title);
            command.Parameters.AddWithValue("$paidBy", expense.PaidByParticipantId);
            command.Parameters.AddWithValue("$amountMinor", expense.AmountMinor);
            command.Parameters.AddWithValue("$date", expense.Date);
            command.Parameters.AddWithValue("$split", SplitJson.SerializeDefinition(expense.SplitDefinition));
            command.Parameters.AddWithValue("$notes", (object?)expense.Notes ?? DBNull.Value);
            command.ExecuteNonQuery();
            await Task.CompletedTask;
        });

    public Task DeleteAsync(string groupId, string expenseId, CancellationToken cancellationToken)
        => _transactionRunner.RunInTransactionAsync(async () =>
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM expenses WHERE group_id = $groupId AND id = $id";
            command.Parameters.AddWithValue("$groupId", groupId);
            command.Parameters.AddWithValue("$id", expenseId);
            command.ExecuteNonQuery();
            await Task.CompletedTask;
        });
}

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
                ParseTransferType(reader.GetString(6)),
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
            command.Parameters.AddWithValue("$type", ToTransferTypeString(transfer.Type));
            command.Parameters.AddWithValue("$note", (object?)transfer.Note ?? DBNull.Value);
            command.ExecuteNonQuery();
            await Task.CompletedTask;
        });

    private static TransferType ParseTransferType(string value)
        => value switch
        {
            "GENERATED" => TransferType.Generated,
            "MANUAL" => TransferType.Manual,
            _ => throw new InvalidOperationException($"Unknown transfer type: {value}")
        };

    private static string ToTransferTypeString(TransferType value)
        => value switch
        {
            TransferType.Generated => "GENERATED",
            TransferType.Manual => "MANUAL",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown transfer type")
        };
}

internal static class SplitJson
{
    public static string SerializeDefinition(SplitDefinition definition)
        => JsonSerializer.Serialize(ToDto(definition));

    public static SplitDefinition ParseDefinition(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("components", out var componentsElement) || componentsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Invalid split definition JSON: missing components");
        }

        var components = new List<SplitComponent>();
        foreach (var componentElement in componentsElement.EnumerateArray())
        {
            var type = componentElement.GetProperty("type").GetString();
            if (string.Equals(type, "FIXED", StringComparison.Ordinal))
            {
                var shares = new Dictionary<string, long>(StringComparer.Ordinal);
                foreach (var property in componentElement.GetProperty("shares").EnumerateObject())
                {
                    shares[property.Name] = property.Value.GetInt64();
                }

                components.Add(new FixedSplitComponent(shares));
                continue;
            }

            var participants = componentElement.GetProperty("participants")
                .EnumerateArray()
                .Select(value => value.GetString() ?? string.Empty)
                .ToArray();

            var mode = componentElement.GetProperty("mode").GetString() switch
            {
                "EQUAL" => RemainderMode.Equal,
                "WEIGHT" => RemainderMode.Weight,
                "PERCENT" => RemainderMode.Percent,
                var unknown => throw new InvalidOperationException($"Unknown split remainder mode: {unknown}")
            };

            Dictionary<string, string>? weights = null;
            if (componentElement.TryGetProperty("weights", out var weightsElement) && weightsElement.ValueKind == JsonValueKind.Object)
            {
                weights = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in weightsElement.EnumerateObject())
                {
                    weights[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }

            Dictionary<string, int>? percents = null;
            if (componentElement.TryGetProperty("percents", out var percentsElement) && percentsElement.ValueKind == JsonValueKind.Object)
            {
                percents = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var property in percentsElement.EnumerateObject())
                {
                    percents[property.Name] = property.Value.GetInt32();
                }
            }

            components.Add(new RemainderSplitComponent(participants, mode, weights, percents));
        }

        return new SplitDefinition(components);
    }

    private static object ToDto(SplitDefinition definition)
        => new
        {
            components = definition.Components.Select(ToComponentDto).ToArray()
        };

    private static object ToComponentDto(SplitComponent component)
        => component switch
        {
            FixedSplitComponent fixedComponent => new
            {
                type = "FIXED",
                shares = fixedComponent.Shares
            },
            RemainderSplitComponent remainderComponent => new
            {
                type = "REMAINDER",
                participants = remainderComponent.Participants,
                mode = remainderComponent.Mode switch
                {
                    RemainderMode.Equal => "EQUAL",
                    RemainderMode.Weight => "WEIGHT",
                    RemainderMode.Percent => "PERCENT",
                    _ => throw new ArgumentOutOfRangeException(nameof(remainderComponent.Mode), remainderComponent.Mode, "Unknown mode")
                },
                weights = remainderComponent.Weights,
                percents = remainderComponent.Percents
            },
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown split component")
        };
}
