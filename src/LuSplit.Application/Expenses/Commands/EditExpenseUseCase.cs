using System.Text.Json;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Expenses.Ports;
using LuSplit.Application.Expenses.Models;
using LuSplit.Application.Groups.Models;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Commands;
using LuSplit.Application.Shared.Ports;
using LuSplit.Application.Sync.Ports;
using LuSplit.Contracts.Sync.Payloads;
using LuSplit.Domain.Expenses;
using LuSplit.Domain.Sync;

namespace LuSplit.Application.Expenses.Commands;

public sealed class EditExpenseUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IParticipantRepository _participantRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IIdGenerator? _idGenerator;
    private readonly IClock? _clock;
    private readonly IOperationRepository? _operationRepository;
    private readonly ISharedGroupStateRepository? _sharedGroupStateRepository;

    public EditExpenseUseCase(
        IGroupRepository groupRepository,
        IParticipantRepository participantRepository,
        IExpenseRepository expenseRepository,
        IIdGenerator? idGenerator = null,
        IClock? clock = null,
        IOperationRepository? operationRepository = null,
        ISharedGroupStateRepository? sharedGroupStateRepository = null)
    {
        _groupRepository = groupRepository;
        _participantRepository = participantRepository;
        _expenseRepository = expenseRepository;
        _idGenerator = idGenerator;
        _clock = clock;
        _operationRepository = operationRepository;
        _sharedGroupStateRepository = sharedGroupStateRepository;
    }

    public async Task<ExpenseModel> ExecuteAsync(EditExpenseInput input, CancellationToken cancellationToken = default)
    {
        UseCaseGuards.AssertNonEmpty(input.GroupId, "groupId");
        UseCaseGuards.AssertNonEmpty(input.ExpenseId, "expenseId");

        var group = await _groupRepository.GetByIdAsync(input.GroupId, cancellationToken);
        if (group is null)
        {
            throw new NotFoundError($"Group not found: {input.GroupId}");
        }

        if (group.Closed)
        {
            throw new ValidationError($"Group is closed: {group.Id}");
        }

        var existing = await _expenseRepository.GetExpenseByIdAsync(input.ExpenseId, cancellationToken);
        if (existing is null || !string.Equals(existing.GroupId, input.GroupId, StringComparison.Ordinal))
        {
            throw new NotFoundError($"Expense not found: {input.ExpenseId}");
        }

        if (input.AmountMinor is <= 0)
        {
            throw new ValidationError("amountMinor must be greater than zero");
        }

        var nextDate = UseCaseGuards.ResolveDate(input.Date, existing.Date);

        var nextExpense = existing with
        {
            Title = input.Title ?? existing.Title,
            PaidByParticipantId = input.PaidByParticipantId ?? existing.PaidByParticipantId,
            AmountMinor = input.AmountMinor ?? existing.AmountMinor,
            SplitDefinition = input.SplitDefinition ?? existing.SplitDefinition,
            Date = nextDate,
            Notes = input.Notes ?? existing.Notes
        };

        var participants = await _participantRepository.ListParticipantsByGroupIdAsync(input.GroupId, cancellationToken);
        var payerExists = participants.Any(p => string.Equals(p.Id, nextExpense.PaidByParticipantId, StringComparison.Ordinal));
        if (!payerExists)
        {
            throw new ValidationError($"Payer is not in group {input.GroupId}");
        }

        var evaluatedShares = SplitEvaluator.EvaluateSplit(nextExpense, participants);
        await _expenseRepository.SaveAsync(nextExpense, cancellationToken);

        await EnqueueOperationIfSharedAsync(nextExpense, evaluatedShares, cancellationToken);

        return new ExpenseModel(
            nextExpense.Id,
            nextExpense.GroupId,
            nextExpense.Title,
            nextExpense.PaidByParticipantId,
            nextExpense.AmountMinor,
            nextExpense.Date,
            nextExpense.SplitDefinition,
            nextExpense.Notes);
    }

    private async Task EnqueueOperationIfSharedAsync(
        Expense expense, IReadOnlyDictionary<string, long> shares, CancellationToken ct)
    {
        if (_operationRepository is null || _sharedGroupStateRepository is null || _idGenerator is null || _clock is null) return;
        var sharedState = await _sharedGroupStateRepository.GetByGroupIdAsync(expense.GroupId, ct);
        if (sharedState is null || !sharedState.IsShared) return;

        var splits = shares.Select(kvp => new SplitLinePayload(kvp.Key, kvp.Value / 100m)).ToList();
        var payload = new EditExpensePayload(
            expense.Id, expense.Title, expense.AmountMinor / 100m, "",
            expense.PaidByParticipantId, _clock.UtcNow, splits);
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);

        var operation = new Operation(
            _idGenerator.NextId(), expense.GroupId, "", "",
            _clock.UtcNow.Ticks.ToString("D20"),
            OperationType.EditExpense, expense.Id,
            payloadBytes, sharedState.CurrentKeyVersion, _clock.UtcNow);

        await _operationRepository.SaveAsync(operation, ct);
    }
}
