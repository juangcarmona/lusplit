using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Expenses.Ports;
using LuSplit.Application.Expenses.Models;
using LuSplit.Application.Groups.Models;
using LuSplit.Application.Groups.Ports;

namespace LuSplit.Application.Expenses.Queries;

public sealed class GetExpensesUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IExpenseRepository _expenseRepository;

    public GetExpensesUseCase(IGroupRepository groupRepository, IExpenseRepository expenseRepository)
    {
        _groupRepository = groupRepository;
        _expenseRepository = expenseRepository;
    }

    public async Task<IReadOnlyList<ExpenseModel>> ExecuteAsync(string groupId, CancellationToken cancellationToken = default)
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

        var expenses = await _expenseRepository.ListExpensesByGroupIdAsync(groupId, cancellationToken);
        return expenses
            .Select(expense => new ExpenseModel(
                expense.Id,
                expense.GroupId,
                expense.Title,
                expense.PaidByParticipantId,
                expense.AmountMinor,
                expense.Date,
                expense.SplitDefinition,
                expense.Notes))
            .ToArray();
    }
}
