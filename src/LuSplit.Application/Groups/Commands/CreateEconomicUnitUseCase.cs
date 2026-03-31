using LuSplit.Application.Groups.Models;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Commands;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Shared.Ports;
using LuSplit.Domain.Groups;

namespace LuSplit.Application.Groups.Commands;

public sealed class CreateEconomicUnitUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IEconomicUnitRepository _economicUnitRepository;
    private readonly IIdGenerator _idGenerator;

    public CreateEconomicUnitUseCase(
        IGroupRepository groupRepository,
        IEconomicUnitRepository economicUnitRepository,
        IIdGenerator idGenerator)
    {
        _groupRepository = groupRepository;
        _economicUnitRepository = economicUnitRepository;
        _idGenerator = idGenerator;
    }

    public async Task<EconomicUnitModel> ExecuteAsync(CreateEconomicUnitInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.GroupId))
        {
            throw new ValidationError("groupId is required");
        }

        if (string.IsNullOrWhiteSpace(input.OwnerParticipantId))
        {
            throw new ValidationError("ownerParticipantId is required");
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

        var economicUnit = new EconomicUnit(
            _idGenerator.NextId(),
            input.GroupId,
            input.OwnerParticipantId,
            input.Name);

        await _economicUnitRepository.SaveEconomicUnitAsync(economicUnit, cancellationToken);

        return new EconomicUnitModel(
            economicUnit.Id,
            economicUnit.GroupId,
            economicUnit.OwnerParticipantId,
            economicUnit.Name);
    }
}
