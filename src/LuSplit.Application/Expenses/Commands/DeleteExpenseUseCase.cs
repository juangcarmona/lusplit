using System.Text.Json;
using LuSplit.Application.Expenses.Ports;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Commands;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Shared.Ports;
using LuSplit.Application.Sync.Ports;
using LuSplit.Contracts.Sync.Payloads;
using LuSplit.Domain.Sync;

namespace LuSplit.Application.Expenses.Commands;

public sealed class DeleteExpenseUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IIdGenerator? _idGenerator;
    private readonly IClock? _clock;
    private readonly IOperationRepository? _operationRepository;
    private readonly ISharedGroupStateRepository? _sharedGroupStateRepository;

    public DeleteExpenseUseCase(
        IGroupRepository groupRepository,
        IExpenseRepository expenseRepository,
        IIdGenerator? idGenerator = null,
        IClock? clock = null,
        IOperationRepository? operationRepository = null,
        ISharedGroupStateRepository? sharedGroupStateRepository = null)
    {
        _groupRepository = groupRepository;
        _expenseRepository = expenseRepository;
        _idGenerator = idGenerator;
        _clock = clock;
        _operationRepository = operationRepository;
        _sharedGroupStateRepository = sharedGroupStateRepository;
    }

    public async Task ExecuteAsync(DeleteExpenseInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.GroupId))
        {
            throw new ValidationError("groupId is required");
        }

        if (string.IsNullOrWhiteSpace(input.ExpenseId))
        {
            throw new ValidationError("expenseId is required");
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

        var expense = await _expenseRepository.GetExpenseByIdAsync(input.ExpenseId, cancellationToken);
        if (expense is null || !string.Equals(expense.GroupId, input.GroupId, StringComparison.Ordinal))
        {
            throw new NotFoundError($"Expense not found: {input.ExpenseId}");
        }

        await _expenseRepository.DeleteAsync(input.GroupId, input.ExpenseId, cancellationToken);

        await EnqueueOperationIfSharedAsync(input.GroupId, input.ExpenseId, cancellationToken);
    }

    private async Task EnqueueOperationIfSharedAsync(string groupId, string expenseId, CancellationToken ct)
    {
        if (_operationRepository is null || _sharedGroupStateRepository is null || _idGenerator is null || _clock is null) return;
        var sharedState = await _sharedGroupStateRepository.GetByGroupIdAsync(groupId, ct);
        if (sharedState is null || !sharedState.IsShared) return;

        var payload = new DeleteExpensePayload(expenseId);
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);

        var operation = new Operation(
            _idGenerator.NextId(), groupId, "", "",
            _clock.UtcNow.Ticks.ToString("D20"),
            OperationType.DeleteExpense, expenseId,
            payloadBytes, sharedState.CurrentKeyVersion, _clock.UtcNow);

        await _operationRepository.SaveAsync(operation, ct);
    }
}
