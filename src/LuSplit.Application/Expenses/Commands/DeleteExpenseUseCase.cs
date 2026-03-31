using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Expenses.Ports;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Commands;
using LuSplit.Application.Shared.Errors;

namespace LuSplit.Application.Expenses.Commands;

public sealed class DeleteExpenseUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IExpenseRepository _expenseRepository;

    public DeleteExpenseUseCase(IGroupRepository groupRepository, IExpenseRepository expenseRepository)
    {
        _groupRepository = groupRepository;
        _expenseRepository = expenseRepository;
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
    }
}
