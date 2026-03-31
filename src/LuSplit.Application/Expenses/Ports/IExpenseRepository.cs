using LuSplit.Domain.Expenses;

namespace LuSplit.Application.Expenses.Ports;

public interface IExpenseRepository
{
    Task<IReadOnlyList<Expense>> ListExpensesByGroupIdAsync(string groupId, CancellationToken cancellationToken);

    Task<Expense?> GetExpenseByIdAsync(string expenseId, CancellationToken cancellationToken);

    Task SaveAsync(Expense expense, CancellationToken cancellationToken);

    Task DeleteAsync(string groupId, string expenseId, CancellationToken cancellationToken);
}
