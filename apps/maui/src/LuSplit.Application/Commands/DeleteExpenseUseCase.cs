using LuSplit.Application.Errors;
using LuSplit.Application.Ports;

namespace LuSplit.Application.Commands;

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
            throw new ValidationError("GroupId is required");
        }

        if (string.IsNullOrWhiteSpace(input.ExpenseId))
        {
            throw new ValidationError("ExpenseId is required");
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
