using LuSplit.Application.Errors;
using LuSplit.Application.Models;
using LuSplit.Application.Ports;
using LuSplit.Domain.Balance;

namespace LuSplit.Application.Queries;

public sealed class GetBalancesByEconomicUnitOwnerUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IParticipantRepository _participantRepository;
    private readonly IEconomicUnitRepository _economicUnitRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly ITransferRepository _transferRepository;

    public GetBalancesByEconomicUnitOwnerUseCase(
        IGroupRepository groupRepository,
        IParticipantRepository participantRepository,
        IEconomicUnitRepository economicUnitRepository,
        IExpenseRepository expenseRepository,
        ITransferRepository transferRepository)
    {
        _groupRepository = groupRepository;
        _participantRepository = participantRepository;
        _economicUnitRepository = economicUnitRepository;
        _expenseRepository = expenseRepository;
        _transferRepository = transferRepository;
    }

    public async Task<IReadOnlyList<BalanceModel>> ExecuteAsync(string groupId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            throw new ValidationError("groupId is required");
        }

        var group = await _groupRepository.GetByIdAsync(groupId, cancellationToken);
        if (group is null)
        {
            throw new NotFoundError($"Group not found: {groupId}");
        }

        var participants = await _participantRepository.ListParticipantsByGroupIdAsync(groupId, cancellationToken);
        var economicUnits = await _economicUnitRepository.ListEconomicUnitsByGroupIdAsync(groupId, cancellationToken);
        var expenses = await _expenseRepository.ListExpensesByGroupIdAsync(groupId, cancellationToken);
        var transfers = await _transferRepository.ListTransfersByGroupIdAsync(groupId, cancellationToken);
        var participantBalances = BalanceCalculator.CalculateParticipantBalances(expenses, transfers, participants);
        var ownerBalances = BalanceCalculator.AggregateBalancesByEconomicUnitOwner(participantBalances, participants, economicUnits);

        return ownerBalances
            .Select(entry => new BalanceModel(entry.Key, entry.Value))
            .OrderBy(entry => entry.EntityId, StringComparer.Ordinal)
            .ToArray();
    }
}
