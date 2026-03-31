using LuSplit.Application.Commands;
using LuSplit.Application.Queries;
using LuSplit.App.Resources.Localization;
using LuSplit.Domain.Entities;
using LuSplit.Infrastructure.Client;

namespace LuSplit.App.Services;

/// <summary>
/// Handles persistence for groups, economic units, participants, and the
/// app-layer group_metadata SQL table. The <c>getInfra</c> delegate
/// resolves the initialized <see cref="InfraLocalSqlite"/> instance; the
/// <c>idGenerator</c> is the shared generator owned by
/// <see cref="AppDataService"/> and reused here to keep IDs sequential
/// within a single operation.
/// </summary>
internal sealed class GroupPersistenceService
{
    private readonly Func<Task<InfraLocalSqlite>> _getInfra;
    private readonly GuidIdGenerator _idGenerator;

    internal GroupPersistenceService(
        Func<Task<InfraLocalSqlite>> getInfra,
        GuidIdGenerator idGenerator)
    {
        _getInfra = getInfra;
        _idGenerator = idGenerator;
    }

    // -------------------------------------------------------------------------
    // Group metadata SQL helpers
    // -------------------------------------------------------------------------

    internal async Task<IReadOnlyList<string>> ListGroupIdsAsync()
    {
        var infra = await _getInfra();

        using var command = infra.Db.CreateCommand();
        command.CommandText = "SELECT id FROM groups ORDER BY id";

        var groupIds = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            groupIds.Add(reader.GetString(0));
        }

        return groupIds;
    }

    internal async Task<GroupMetadataRecord> GetGroupMetadataAsync(string groupId)
    {
        var infra = await _getInfra();

        using var command = infra.Db.CreateCommand();
        command.CommandText = "SELECT name, last_opened_utc, image_path FROM group_metadata WHERE group_id = $groupId";
        command.Parameters.AddWithValue("$groupId", groupId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return new GroupMetadataRecord(null, null);
        }

        return new GroupMetadataRecord(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) || !DateTimeOffset.TryParse(reader.GetString(1), out var lastOpenedUtc) ? null : lastOpenedUtc,
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    internal async Task SaveGroupMetadataAsync(string groupId, string? name, DateTimeOffset? lastOpenedUtc)
    {
        var infra = await _getInfra();

        using var command = infra.Db.CreateCommand();
        command.CommandText = @"
INSERT INTO group_metadata (group_id, name, last_opened_utc)
VALUES ($groupId, $name, $lastOpenedUtc)
ON CONFLICT(group_id) DO UPDATE SET
  name = COALESCE(excluded.name, group_metadata.name),
  last_opened_utc = COALESCE(excluded.last_opened_utc, group_metadata.last_opened_utc);";
        command.Parameters.AddWithValue("$groupId", groupId);
        command.Parameters.AddWithValue("$name", (object?)name ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastOpenedUtc", lastOpenedUtc?.ToString("O") ?? (object)DBNull.Value);
        command.ExecuteNonQuery();

        await Task.CompletedTask;
    }

    internal Task TouchGroupAsync(string groupId)
        => SaveGroupMetadataAsync(groupId, null, DateTimeOffset.UtcNow);

    internal async Task SaveGroupImageAsync(string groupId, string? imagePath)
    {
        var infra = await _getInfra();

        using var command = infra.Db.CreateCommand();
        command.CommandText = @"
INSERT INTO group_metadata (group_id, image_path)
VALUES ($groupId, $imagePath)
ON CONFLICT(group_id) DO UPDATE SET image_path = excluded.image_path;";
        command.Parameters.AddWithValue("$groupId", groupId);
        command.Parameters.AddWithValue("$imagePath", (object?)imagePath ?? DBNull.Value);
        command.ExecuteNonQuery();

        await Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Group commands
    // -------------------------------------------------------------------------

    internal async Task UpdateGroupAsync(string groupId, string normalizedName, string normalizedCurrency)
    {
        var infra = await _getInfra();
        var existing = await infra.GroupRepository.GetByIdAsync(groupId, CancellationToken.None)
            ?? throw new InvalidOperationException(AppResources.Validation_GroupNotFound);

        await infra.GroupRepository.SaveGroupAsync(existing with { Currency = normalizedCurrency }, CancellationToken.None);

        var metadata = await GetGroupMetadataAsync(groupId);
        await SaveGroupMetadataAsync(groupId, normalizedName, metadata.LastOpenedUtc);
    }

    /// <summary>Closes the group via use-case. Does not touch AppDataService selection state.</summary>
    internal async Task ArchiveGroupCoreAsync(string groupId)
    {
        var infra = await _getInfra();
        await new CloseGroupUseCase(infra.GroupRepository).ExecuteAsync(new CloseGroupInput(groupId));
    }

    // -------------------------------------------------------------------------
    // Participant / member commands
    // -------------------------------------------------------------------------

    internal async Task AddGroupMemberAsync(
        string groupId,
        string normalizedName,
        string? normalizedHouseholdName,
        ConsumptionCategory consumptionCategory,
        string? customConsumptionWeight)
    {
        var infra = await _getInfra();
        var participants = await infra.ParticipantRepository
            .ListParticipantsByGroupIdAsync(groupId, CancellationToken.None);

        if (participants.Any(p => string.Equals(p.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(AppResources.Validation_PersonNameMustBeUnique);
        }

        await AddMembersAsync(groupId, new[]
        {
            new GroupDraftMember(normalizedName, normalizedHouseholdName, consumptionCategory, customConsumptionWeight)
        });
    }

    internal async Task UpdateGroupMemberAsync(
        string groupId,
        string normalizedParticipantId,
        string normalizedName,
        string? normalizedDependsOnParticipantId)
    {
        var infra = await _getInfra();
        var group = await infra.GroupRepository.GetByIdAsync(groupId, CancellationToken.None)
            ?? throw new InvalidOperationException(AppResources.Validation_GroupNotFound);
        if (group.Closed)
        {
            throw new InvalidOperationException(AppResources.Validation_GroupArchivedReadonly);
        }

        var participants = await infra.ParticipantRepository
            .ListParticipantsByGroupIdAsync(groupId, CancellationToken.None);
        var participant = participants.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, normalizedParticipantId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(AppResources.Validation_PersonNotFound);

        if (participants.Any(candidate =>
                !string.Equals(candidate.Id, participant.Id, StringComparison.Ordinal)
                && string.Equals(candidate.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(AppResources.Validation_PersonNameMustBeUnique);
        }

        var economicUnits = await infra.EconomicUnitRepository
            .ListEconomicUnitsByGroupIdAsync(groupId, CancellationToken.None);
        var currentUnit = economicUnits.FirstOrDefault(unit =>
            string.Equals(unit.Id, participant.EconomicUnitId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(AppResources.Validation_PersonNotFound);

        var isCurrentlyOwner = string.Equals(currentUnit.OwnerParticipantId, participant.Id, StringComparison.Ordinal);

        EconomicUnit destinationUnit;
        string? unitToDeleteId = null;

        if (string.IsNullOrWhiteSpace(normalizedDependsOnParticipantId))
        {
            // Clearing dependency: participant should own their own unit.
            if (isCurrentlyOwner)
            {
                // Already in their own unit — nothing to change structurally.
                destinationUnit = currentUnit;
            }
            else
            {
                // Was a dependent: create a new solo unit for them.
                var newUnit = new EconomicUnit(
                    _idGenerator.NextId(),
                    groupId,
                    participant.Id,
                    null);
                await infra.EconomicUnitRepository.SaveEconomicUnitAsync(newUnit, CancellationToken.None);
                destinationUnit = newUnit;
            }
        }
        else
        {
            // Setting a new responsible: participant becomes a dependent.
            var responsibleParticipant = participants.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, normalizedDependsOnParticipantId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(AppResources.Validation_ResponsiblePersonNotFound);

            if (string.Equals(responsibleParticipant.Id, participant.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(AppResources.Validation_PersonCannotDependOnSelf);
            }

            destinationUnit = economicUnits.FirstOrDefault(unit =>
                string.Equals(unit.Id, responsibleParticipant.EconomicUnitId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(AppResources.Validation_ResponsiblePersonNotFound);

            // If participant owned their own unit, check no other participants are still in it.
            if (isCurrentlyOwner)
            {
                var othersInOldUnit = participants.Any(p =>
                    !string.Equals(p.Id, participant.Id, StringComparison.Ordinal)
                    && string.Equals(p.EconomicUnitId, currentUnit.Id, StringComparison.Ordinal));

                if (othersInOldUnit)
                {
                    throw new InvalidOperationException(AppResources.Validation_CannotMakeDependentWhileOwningOthers);
                }

                // Mark old unit for deletion after participant is moved out.
                unitToDeleteId = currentUnit.Id;
            }
        }

        var updatedParticipant = participant with
        {
            Name = normalizedName,
            EconomicUnitId = destinationUnit.Id
        };
        await infra.ParticipantRepository.SaveParticipantAsync(updatedParticipant, CancellationToken.None);

        if (unitToDeleteId is not null)
        {
            await infra.EconomicUnitRepository.DeleteEconomicUnitAsync(unitToDeleteId, CancellationToken.None);
        }
    }

    internal async Task AddMembersAsync(string groupId, IReadOnlyList<GroupDraftMember> members)
    {
        var infra = await _getInfra();
        var createEconomicUnit = new CreateEconomicUnitUseCase(infra.GroupRepository, infra.EconomicUnitRepository, _idGenerator);
        var createParticipant = new CreateParticipantUseCase(infra.GroupRepository, infra.EconomicUnitRepository, infra.ParticipantRepository, _idGenerator);

        // Resolve existing economic units by name to support household grouping.
        // Uses GetGroupOverviewUseCase directly rather than the AppDataService facade
        // to avoid a circular delegate dependency.
        var overview = await new GetGroupOverviewUseCase(
            infra.GroupRepository,
            infra.ParticipantRepository,
            infra.EconomicUnitRepository,
            infra.ExpenseRepository,
            infra.TransferRepository).ExecuteAsync(groupId);

        var unitsByName = overview.EconomicUnits
            .Where(unit => !string.IsNullOrWhiteSpace(unit.Name))
            .GroupBy(unit => unit.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var member in members)
        {
            var householdName = string.IsNullOrWhiteSpace(member.HouseholdName)
                ? null : member.HouseholdName.Trim();

            Application.Models.EconomicUnitModel unit;

            if (!string.IsNullOrWhiteSpace(householdName) && unitsByName.TryGetValue(householdName, out var existingUnit))
            {
                unit = existingUnit;
            }
            else
            {
                unit = await createEconomicUnit.ExecuteAsync(new CreateEconomicUnitInput(
                    groupId,
                    _idGenerator.NextId(),
                    householdName));

                if (!string.IsNullOrWhiteSpace(householdName))
                {
                    unitsByName[householdName] = unit;
                }
            }

            await createParticipant.ExecuteAsync(new CreateParticipantInput(
                groupId,
                unit.Id,
                member.Name,
                member.ConsumptionCategory,
                member.CustomConsumptionWeight));
        }
    }
}
