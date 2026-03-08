using LuSplit.Application.Commands;
using LuSplit.Domain.Entities;

namespace LuSplit.Application.Ports;

public interface IExpenseRepository
{
    Task AddAsync(AddExpenseCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<Expense>> ListExpensesByGroupIdAsync(string groupId, CancellationToken cancellationToken);

    Task<Expense?> GetExpenseByIdAsync(string expenseId, CancellationToken cancellationToken);

    Task SaveAsync(Expense expense, CancellationToken cancellationToken);

    Task DeleteAsync(string groupId, string expenseId, CancellationToken cancellationToken);
}
