using LuSplit.Application.Errors;
using LuSplit.Application.Models;
using LuSplit.Application.Ports;
using LuSplit.Domain.Balance;
using LuSplit.Domain.Settlement;

namespace LuSplit.Application.Queries;

public sealed class GetSettlementPlanUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IParticipantRepository _participantRepository;
    private readonly IEconomicUnitRepository _economicUnitRepository;
    private readonly IExpenseRepository _expenseRepository;

    public GetSettlementPlanUseCase(
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

    public async Task<SettlementPlanModel> ExecuteAsync(
        string groupId,
        SettlementMode mode,
        CancellationToken cancellationToken = default)
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
        var expenses = await _expenseRepository.ListExpensesByGroupIdAsync(groupId, cancellationToken);
        var participantBalances = BalanceCalculator.CalculateParticipantBalances(expenses, participants);

        IReadOnlyDictionary<string, long> balances = mode switch
        {
            SettlementMode.Participant => participantBalances,
            SettlementMode.EconomicUnitOwner => BalanceCalculator.AggregateBalancesByEconomicUnitOwner(
                participantBalances,
                participants,
                await _economicUnitRepository.ListEconomicUnitsByGroupIdAsync(groupId, cancellationToken)),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown settlement mode")
        };

        var transfers = SettlementPlanner.PlanSettlement(balances)
            .Select(transfer => new SettlementTransferModel(
                transfer.FromParticipantId,
                transfer.ToParticipantId,
                transfer.AmountMinor))
            .ToArray();

        return new SettlementPlanModel(mode, transfers);
    }
}
