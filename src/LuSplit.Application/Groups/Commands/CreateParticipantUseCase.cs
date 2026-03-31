using LuSplit.Application.Groups.Models;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Commands;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Shared.Ports;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.Groups.Commands;

public sealed class CreateParticipantUseCase
{
    private const int MaxIdGenerationAttempts = 100;

    private readonly IGroupRepository _groupRepository;
    private readonly IEconomicUnitRepository _economicUnitRepository;
    private readonly IParticipantRepository _participantRepository;
    private readonly IIdGenerator _idGenerator;

    public CreateParticipantUseCase(
        IGroupRepository groupRepository,
        IEconomicUnitRepository economicUnitRepository,
        IParticipantRepository participantRepository,
        IIdGenerator idGenerator)
    {
        _groupRepository = groupRepository;
        _economicUnitRepository = economicUnitRepository;
        _participantRepository = participantRepository;
        _idGenerator = idGenerator;
    }

    public async Task<ParticipantModel> ExecuteAsync(CreateParticipantInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.GroupId))
        {
            throw new ValidationError("groupId is required");
        }

        if (string.IsNullOrWhiteSpace(input.EconomicUnitId))
        {
            throw new ValidationError("economicUnitId is required");
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationError("name is required");
        }

        if (input.ConsumptionCategory == ConsumptionCategory.Custom && string.IsNullOrWhiteSpace(input.CustomConsumptionWeight))
        {
            throw new ValidationError("customConsumptionWeight is required for CUSTOM consumptionCategory");
        }

        var group = await _groupRepository.GetByIdAsync(input.GroupId, cancellationToken);
        if (group is null)
        {
            throw new NotFoundError($"Group not found: {input.GroupId}");
        }

        if (group.Closed)
        {
            throw new ValidationError($"Group is closed: {group.Id}");
        }

        var economicUnit = await _economicUnitRepository.GetEconomicUnitByIdAsync(input.EconomicUnitId, cancellationToken);
        if (economicUnit is null || !string.Equals(economicUnit.GroupId, input.GroupId, StringComparison.Ordinal))
        {
            throw new ValidationError($"Economic unit is not in group {input.GroupId}");
        }

        if (string.IsNullOrWhiteSpace(economicUnit.OwnerParticipantId))
        {
            throw new ValidationError("ownerParticipantId is required");
        }

        var participants = await _participantRepository.ListParticipantsByGroupIdAsync(input.GroupId, cancellationToken);
        var ownerId = economicUnit.OwnerParticipantId;
        var unitId = economicUnit.Id;
        var ownerExists = participants.Any(p => string.Equals(p.Id, ownerId, StringComparison.Ordinal));
        var unitHasParticipants = participants.Any(p => string.Equals(p.EconomicUnitId, unitId, StringComparison.Ordinal));

        string participantId;
        if (!ownerExists && !unitHasParticipants)
        {
            participantId = ownerId;
        }
        else
        {
            var ids = participants.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
            var attempts = 0;
            participantId = _idGenerator.NextId();
            while (ids.Contains(participantId) && attempts < MaxIdGenerationAttempts)
            {
                attempts += 1;
                participantId = _idGenerator.NextId();
            }

            if (ids.Contains(participantId))
            {
                throw new ValidationError("Unable to generate a unique participant id");
            }
        }

        var participant = new Participant(
            participantId,
            input.GroupId,
            input.EconomicUnitId,
            input.Name,
            input.ConsumptionCategory,
            input.CustomConsumptionWeight);

        await _participantRepository.SaveParticipantAsync(participant, cancellationToken);

        return new ParticipantModel(
            participant.Id,
            participant.GroupId,
            participant.EconomicUnitId,
            participant.Name,
            participant.ConsumptionCategory.ToString().ToUpperInvariant(),
            participant.CustomConsumptionWeight);
    }
}
