using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Expenses.Ports;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Payments.Models;
using LuSplit.Application.Payments.Ports;
using LuSplit.Domain.Payments;

namespace LuSplit.Application.Payments.Queries;

public sealed class GetSettlementPlanUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IParticipantRepository _participantRepository;
    private readonly IEconomicUnitRepository _economicUnitRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly ITransferRepository _transferRepository;

    public GetSettlementPlanUseCase(
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
        var recordedTransfers = await _transferRepository.ListTransfersByGroupIdAsync(groupId, cancellationToken);

        var participantBalances = BalanceCalculator.CalculateParticipantBalances(
            expenses,
            recordedTransfers,
            participants);

        IReadOnlyDictionary<string, long> balances = mode switch
        {
            SettlementMode.Participant => participantBalances,
            SettlementMode.EconomicUnitOwner => BalanceCalculator.AggregateBalancesByEconomicUnitOwner(
                participantBalances,
                participants,
                await _economicUnitRepository.ListEconomicUnitsByGroupIdAsync(groupId, cancellationToken)),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown settlement mode")
        };

        var plannedTransfers = SettlementPlanner.PlanSettlement(balances)
            .Select(transfer => new SettlementTransferModel(
                transfer.FromParticipantId,
                transfer.ToParticipantId,
                transfer.AmountMinor))
            .ToArray();

        return new SettlementPlanModel(mode, plannedTransfers);
    }
}