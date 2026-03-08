using LuSplit.Application.Errors;
using LuSplit.Application.Models;
using LuSplit.Application.Ports;
using LuSplit.Domain.Balance;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Settlement;

namespace LuSplit.Application.Queries;

public sealed class GetGroupOverviewUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IParticipantRepository _participantRepository;
    private readonly IEconomicUnitRepository _economicUnitRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly ITransferRepository _transferRepository;

    public GetGroupOverviewUseCase(
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

    public async Task<GroupOverviewModel> ExecuteAsync(string groupId, CancellationToken cancellationToken = default)
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

        var balancesByParticipant = BalanceCalculator.CalculateParticipantBalances(expenses, transfers, participants);
        var balancesByEconomicUnitOwner = BalanceCalculator.AggregateBalancesByEconomicUnitOwner(
            balancesByParticipant,
            participants,
            economicUnits);

        var participantBalanceModels = balancesByParticipant
            .Select(entry => new BalanceModel(entry.Key, entry.Value))
            .OrderBy(entry => entry.EntityId, StringComparer.Ordinal)
            .ToArray();

        var ownerBalanceModels = balancesByEconomicUnitOwner
            .Select(entry => new BalanceModel(entry.Key, entry.Value))
            .OrderBy(entry => entry.EntityId, StringComparer.Ordinal)
            .ToArray();

        var settlementByParticipant = new SettlementPlanModel(
            SettlementMode.Participant,
            SettlementPlanner.PlanSettlement(balancesByParticipant)
                .Select(transfer => new SettlementTransferModel(
                    transfer.FromParticipantId,
                    transfer.ToParticipantId,
                    transfer.AmountMinor))
                .ToArray());

        var settlementByEconomicUnitOwner = new SettlementPlanModel(
            SettlementMode.EconomicUnitOwner,
            SettlementPlanner.PlanSettlement(balancesByEconomicUnitOwner)
                .Select(transfer => new SettlementTransferModel(
                    transfer.FromParticipantId,
                    transfer.ToParticipantId,
                    transfer.AmountMinor))
                .ToArray());

        return new GroupOverviewModel(
            new GroupModel(group.Id, group.Currency, group.Closed),
            new GroupSummaryModel(groupId, participants.Count, economicUnits.Count, expenses.Count, transfers.Count),
            participants.Select(MapParticipant).ToArray(),
            economicUnits.Select(unit => new EconomicUnitModel(unit.Id, unit.GroupId, unit.OwnerParticipantId, unit.Name)).ToArray(),
            expenses.Select(expense => new ExpenseModel(
                expense.Id,
                expense.GroupId,
                expense.Title,
                expense.PaidByParticipantId,
                expense.AmountMinor,
                expense.Date,
                expense.SplitDefinition,
                expense.Notes)).ToArray(),
            transfers.Select(MapTransfer).ToArray(),
            participantBalanceModels,
            ownerBalanceModels,
            settlementByParticipant,
            settlementByEconomicUnitOwner);
    }

    private static ParticipantModel MapParticipant(Participant participant)
        => new(
            participant.Id,
            participant.GroupId,
            participant.EconomicUnitId,
            participant.Name,
            participant.ConsumptionCategory.ToString().ToUpperInvariant(),
            participant.CustomConsumptionWeight);

    private static TransferModel MapTransfer(Transfer transfer)
        => new(
            transfer.Id,
            transfer.GroupId,
            transfer.FromParticipantId,
            transfer.ToParticipantId,
            transfer.AmountMinor,
            transfer.Date,
            transfer.Type == TransferType.Manual ? "MANUAL" : "GENERATED",
            transfer.Note);
}
