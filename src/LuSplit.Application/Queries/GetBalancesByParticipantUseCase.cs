using LuSplit.Application.Errors;
using LuSplit.Application.Models;
using LuSplit.Application.Ports;
using LuSplit.Domain.Balance;

namespace LuSplit.Application.Queries;

public sealed class GetBalancesByParticipantUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IParticipantRepository _participantRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly ITransferRepository _transferRepository;

    public GetBalancesByParticipantUseCase(
        IGroupRepository groupRepository,
        IParticipantRepository participantRepository,
        IExpenseRepository expenseRepository,
        ITransferRepository transferRepository)
    {
        _groupRepository = groupRepository;
        _participantRepository = participantRepository;
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
        var expenses = await _expenseRepository.ListExpensesByGroupIdAsync(groupId, cancellationToken);
        var transfers = await _transferRepository.ListTransfersByGroupIdAsync(groupId, cancellationToken);
        var balances = BalanceCalculator.CalculateParticipantBalances(expenses, transfers, participants);

        return balances
            .Select(entry => new BalanceModel(entry.Key, entry.Value))
            .OrderBy(entry => entry.EntityId, StringComparer.Ordinal)
            .ToArray();
    }
}
