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

    public GetBalancesByEconomicUnitOwnerUseCase(
        IGroupRepository groupRepository,
        IParticipantRepository participantRepository,
        IEconomicUnitRepository economicUnitRepository,
        IExpenseRepository expenseRepository)
    {
        _groupRepository = groupRepository;
        _participantRepository = participantRepository;
        _economicUnitRepository = economicUnitRepository;
        _expenseRepository = expenseRepository;
    }

    public async Task<IReadOnlyList<BalanceModel>> ExecuteAsync(string groupId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            throw new ArgumentException("groupId is required", nameof(groupId));
        }

        var group = await _groupRepository.GetByIdAsync(groupId, cancellationToken);
        if (group is null)
        {
            throw new NotFoundError($"Group not found: {groupId}");
        }

        var participants = await _participantRepository.ListParticipantsByGroupIdAsync(groupId, cancellationToken);
        var economicUnits = await _economicUnitRepository.ListEconomicUnitsByGroupIdAsync(groupId, cancellationToken);
        var expenses = await _expenseRepository.ListExpensesByGroupIdAsync(groupId, cancellationToken);
        var participantBalances = BalanceCalculator.CalculateParticipantBalances(expenses, participants);
        var ownerBalances = BalanceCalculator.AggregateBalancesByEconomicUnitOwner(participantBalances, participants, economicUnits);

        return ownerBalances
            .Select(entry => new BalanceModel(entry.Key, entry.Value))
            .OrderBy(entry => entry.EntityId, StringComparer.Ordinal)
            .ToArray();
    }
}
