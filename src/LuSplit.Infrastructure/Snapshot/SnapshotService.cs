using System.Text.Json;
using LuSplit.Domain.Expenses;
using LuSplit.Domain.Groups;
using LuSplit.Domain.Payments;
using LuSplit.Domain.Split;
using LuSplit.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

// Design note: SnapshotService reads from and writes to the database directly rather than
// delegating to the live repositories. This is intentional (Option A): the snapshot path
// is a self-contained export/import concern that must remain stable as an independent
// serialization contract, independent of any filtering or mapping changes made to the
// live read-path repositories. Enum-string conversions are shared via SqliteEnumConverters
// in SqliteHelpers.cs to prevent divergence on those mappings.

namespace LuSplit.Infrastructure.Snapshot;

public static class SnapshotService
{
    public static Task<GroupSnapshotV1> ExportGroupSnapshotAsync(SqliteConnection connection, string groupId)
    {
        var group = ReadGroup(connection, groupId) ?? throw new InvalidOperationException($"Group not found: {groupId}");
        var participants = ReadParticipants(connection, groupId);
        var economicUnits = ReadEconomicUnits(connection, groupId);
        var expenses = ReadExpenses(connection, groupId);
        var transfers = ReadTransfers(connection, groupId);

        return Task.FromResult(new GroupSnapshotV1(
            SnapshotContract.Version,
            new SnapshotGroup(group.Id, group.Currency, group.Closed),
            participants.Select(participant => new SnapshotParticipant(
                participant.Id,
                participant.GroupId,
                participant.EconomicUnitId,
                participant.Name,
                SqliteEnumConverters.ToConsumptionCategoryString(participant.ConsumptionCategory),
                participant.CustomConsumptionWeight)).ToArray(),
            economicUnits.Select(unit => new SnapshotEconomicUnit(unit.Id, unit.GroupId, unit.OwnerParticipantId, unit.Name)).ToArray(),
            expenses.Select(expense => new SnapshotExpense(
                expense.Id,
                expense.GroupId,
                expense.Title,
                expense.PaidByParticipantId,
                expense.AmountMinor,
                expense.Date,
                SplitJson.SerializeDefinitionToElement(expense.SplitDefinition),
                expense.Notes)).ToArray(),
            transfers.Select(transfer => new SnapshotTransfer(
                transfer.Id,
                transfer.GroupId,
                transfer.FromParticipantId,
                transfer.ToParticipantId,
                transfer.AmountMinor,
                transfer.Date,
                transfer.Type == TransferType.Generated ? "GENERATED" : "MANUAL",
                transfer.Note)).ToArray()));
    }

    public static Task ImportGroupSnapshotAsync(SqliteConnection connection, SqliteTransactionRunner transactionRunner, object snapshot)
    {
        var root = snapshot switch
        {
            GroupSnapshotV1 typed => JsonSerializer.SerializeToNode(typed)!,
            _ => JsonSerializer.SerializeToNode(snapshot) ?? throw new InvalidOperationException("Invalid snapshot root")
        };

        var version = root["Version"] ?? root["version"] ?? throw new InvalidOperationException("Missing snapshot field: version");
        if (version.GetValue<int>() != SnapshotContract.Version)
        {
            throw new InvalidOperationException($"Unsupported snapshot version: {version}");
        }

        var groupNode = root["Group"] ?? root["group"] ?? throw new InvalidOperationException("Missing snapshot field: group");
        var participantsNode = root["Participants"] ?? root["participants"] ?? throw new InvalidOperationException("Missing snapshot field: participants");
        var economicUnitsNode = root["EconomicUnits"] ?? root["economicUnits"] ?? throw new InvalidOperationException("Missing snapshot field: economicUnits");
        var expensesNode = root["Expenses"] ?? root["expenses"] ?? throw new InvalidOperationException("Missing snapshot field: expenses");
        var transfersNode = root["Transfers"] ?? root["transfers"] ?? throw new InvalidOperationException("Missing snapshot field: transfers");

        var group = groupNode.Deserialize<SnapshotGroup>() ?? throw new InvalidOperationException("Invalid snapshot group");
        var participants = participantsNode.Deserialize<List<SnapshotParticipant>>() ?? throw new InvalidOperationException("Invalid snapshot participants");
        var economicUnits = economicUnitsNode.Deserialize<List<SnapshotEconomicUnit>>() ?? throw new InvalidOperationException("Invalid snapshot economicUnits");
        var expenses = expensesNode.Deserialize<List<SnapshotExpense>>() ?? throw new InvalidOperationException("Invalid snapshot expenses");
        var transfers = transfersNode.Deserialize<List<SnapshotTransfer>>() ?? throw new InvalidOperationException("Invalid snapshot transfers");

        Validate(group, participants, economicUnits, expenses, transfers);

        return transactionRunner.RunInTransactionAsync(async () =>
        {
            if (ReadGroup(connection, group.Id) is not null)
            {
                throw new InvalidOperationException($"Group already exists: {group.Id}");
            }

            using (var insertGroup = connection.CreateCommand())
            {
                insertGroup.CommandText = "INSERT INTO groups (id, currency, closed) VALUES ($id, $currency, $closed)";
                insertGroup.Parameters.AddWithValue("$id", group.Id);
                insertGroup.Parameters.AddWithValue("$currency", group.Currency);
                insertGroup.Parameters.AddWithValue("$closed", group.Closed ? 1 : 0);
                insertGroup.ExecuteNonQuery();
            }

            foreach (var unit in economicUnits.OrderBy(unit => unit.Id, StringComparer.Ordinal))
            {
                using var insertUnit = connection.CreateCommand();
                insertUnit.CommandText = "INSERT INTO economic_units (id, group_id, owner_participant_id, name) VALUES ($id, $groupId, $ownerId, $name)";
                insertUnit.Parameters.AddWithValue("$id", unit.Id);
                insertUnit.Parameters.AddWithValue("$groupId", unit.GroupId);
                insertUnit.Parameters.AddWithValue("$ownerId", unit.OwnerParticipantId);
                insertUnit.Parameters.AddWithValue("$name", (object?)unit.Name ?? DBNull.Value);
                insertUnit.ExecuteNonQuery();
            }

            foreach (var participant in participants.OrderBy(participant => participant.Id, StringComparer.Ordinal))
            {
                using var insertParticipant = connection.CreateCommand();
                insertParticipant.CommandText = @"INSERT INTO participants (
id, group_id, economic_unit_id, name, consumption_category, custom_consumption_weight
) VALUES ($id, $groupId, $economicUnitId, $name, $category, $weight)";
                insertParticipant.Parameters.AddWithValue("$id", participant.Id);
                insertParticipant.Parameters.AddWithValue("$groupId", participant.GroupId);
                insertParticipant.Parameters.AddWithValue("$economicUnitId", participant.EconomicUnitId);
                insertParticipant.Parameters.AddWithValue("$name", participant.Name);
                insertParticipant.Parameters.AddWithValue("$category", participant.ConsumptionCategory);
                insertParticipant.Parameters.AddWithValue("$weight", (object?)participant.CustomConsumptionWeight ?? DBNull.Value);
                insertParticipant.ExecuteNonQuery();
            }

            foreach (var expense in expenses.OrderBy(expense => expense.Id, StringComparer.Ordinal))
            {
                using var insertExpense = connection.CreateCommand();
                insertExpense.CommandText = @"INSERT INTO expenses (
id, group_id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
) VALUES ($id, $groupId, $title, $paidBy, $amount, $date, $split, $notes)";
                insertExpense.Parameters.AddWithValue("$id", expense.Id);
                insertExpense.Parameters.AddWithValue("$groupId", expense.GroupId);
                insertExpense.Parameters.AddWithValue("$title", expense.Title);
                insertExpense.Parameters.AddWithValue("$paidBy", expense.PaidByParticipantId);
                insertExpense.Parameters.AddWithValue("$amount", expense.AmountMinor);
                insertExpense.Parameters.AddWithValue("$date", expense.Date);
                insertExpense.Parameters.AddWithValue("$split", JsonSerializer.Serialize(expense.SplitDefinition));
                insertExpense.Parameters.AddWithValue("$notes", (object?)expense.Notes ?? DBNull.Value);
                insertExpense.ExecuteNonQuery();
            }

            foreach (var transfer in transfers.OrderBy(transfer => transfer.Id, StringComparer.Ordinal))
            {
                using var insertTransfer = connection.CreateCommand();
                insertTransfer.CommandText = @"INSERT INTO transfers (
id, group_id, from_participant_id, to_participant_id, amount_minor, date, type, note
) VALUES ($id, $groupId, $fromId, $toId, $amount, $date, $type, $note)";
                insertTransfer.Parameters.AddWithValue("$id", transfer.Id);
                insertTransfer.Parameters.AddWithValue("$groupId", transfer.GroupId);
                insertTransfer.Parameters.AddWithValue("$fromId", transfer.FromParticipantId);
                insertTransfer.Parameters.AddWithValue("$toId", transfer.ToParticipantId);
                insertTransfer.Parameters.AddWithValue("$amount", transfer.AmountMinor);
                insertTransfer.Parameters.AddWithValue("$date", transfer.Date);
                insertTransfer.Parameters.AddWithValue("$type", transfer.Type);
                insertTransfer.Parameters.AddWithValue("$note", (object?)transfer.Note ?? DBNull.Value);
                insertTransfer.ExecuteNonQuery();
            }

            await Task.CompletedTask;
        });
    }

    private static void Validate(
        SnapshotGroup group,
        IReadOnlyList<SnapshotParticipant> participants,
        IReadOnlyList<SnapshotEconomicUnit> economicUnits,
        IReadOnlyList<SnapshotExpense> expenses,
        IReadOnlyList<SnapshotTransfer> transfers)
    {
        var participantIds = participants.Select(participant => participant.Id).ToHashSet(StringComparer.Ordinal);
        var economicUnitIds = economicUnits.Select(unit => unit.Id).ToHashSet(StringComparer.Ordinal);

        EnsureUniqueIds(participants.Select(participant => participant.Id).ToArray(), "participant");
        EnsureUniqueIds(economicUnits.Select(unit => unit.Id).ToArray(), "economicUnit");
        EnsureUniqueIds(expenses.Select(expense => expense.Id).ToArray(), "expense");
        EnsureUniqueIds(transfers.Select(transfer => transfer.Id).ToArray(), "transfer");

        foreach (var unit in economicUnits)
        {
            if (!string.Equals(unit.GroupId, group.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Invalid snapshot economicUnit.groupId for {unit.Id}");
            }
        }

        foreach (var participant in participants)
        {
            if (!string.Equals(participant.GroupId, group.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Invalid snapshot participant.groupId for {participant.Id}");
            }

            if (!economicUnitIds.Contains(participant.EconomicUnitId))
            {
                throw new InvalidOperationException($"Invalid snapshot participant.economicUnitId for {participant.Id}");
            }
        }

        foreach (var expense in expenses)
        {
            if (!string.Equals(expense.GroupId, group.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Invalid snapshot expense.groupId for {expense.Id}");
            }

            if (!participantIds.Contains(expense.PaidByParticipantId))
            {
                throw new InvalidOperationException($"Invalid snapshot expense.paidByParticipantId for {expense.Id}");
            }
        }

        foreach (var transfer in transfers)
        {
            if (!string.Equals(transfer.GroupId, group.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Invalid snapshot transfer.groupId for {transfer.Id}");
            }

            if (!participantIds.Contains(transfer.FromParticipantId) || !participantIds.Contains(transfer.ToParticipantId))
            {
                throw new InvalidOperationException($"Invalid snapshot transfer participant reference for {transfer.Id}");
            }
        }
    }

    private static void EnsureUniqueIds(IReadOnlyList<string> ids, string label)
    {
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            throw new InvalidOperationException($"Invalid snapshot duplicate {label} ids");
        }
    }

    private static Group? ReadGroup(SqliteConnection connection, string groupId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, currency, closed FROM groups WHERE id = $id";
        command.Parameters.AddWithValue("$id", groupId);

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new Group(reader.GetString(0), reader.GetString(1), reader.GetInt64(2) == 1)
            : null;
    }

    private static List<Participant> ReadParticipants(SqliteConnection connection, string groupId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, group_id, economic_unit_id, name, consumption_category, custom_consumption_weight
FROM participants WHERE group_id = $groupId ORDER BY id";
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
                SqliteEnumConverters.ParseConsumptionCategory(reader.GetString(4)),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return participants;
    }

    private static List<EconomicUnit> ReadEconomicUnits(SqliteConnection connection, string groupId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, group_id, owner_participant_id, name
FROM economic_units WHERE group_id = $groupId ORDER BY id";
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

        return units;
    }

    private static List<Expense> ReadExpenses(SqliteConnection connection, string groupId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, group_id, title, paid_by_participant_id, amount_minor, date, split_definition_json, notes
FROM expenses WHERE group_id = $groupId ORDER BY id";
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

        return expenses;
    }

    private static List<Transfer> ReadTransfers(SqliteConnection connection, string groupId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"SELECT id, group_id, from_participant_id, to_participant_id, amount_minor, date, type, note
FROM transfers WHERE group_id = $groupId ORDER BY id";
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

        return transfers;
    }
}
