using LuSplit.Application.Commands;
using LuSplit.Domain.Entities;

namespace LuSplit.Application.Ports;

public interface IExpenseRepository
{
    Task AddAsync(AddExpenseCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<Expense>> ListExpensesByGroupIdAsync(string groupId, CancellationToken cancellationToken);
}
