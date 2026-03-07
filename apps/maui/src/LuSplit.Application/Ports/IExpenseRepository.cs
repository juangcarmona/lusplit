using LuSplit.Application.Commands;

namespace LuSplit.Application.Ports;

public interface IExpenseRepository
{
    Task AddAsync(AddExpenseCommand command, CancellationToken cancellationToken);
}
