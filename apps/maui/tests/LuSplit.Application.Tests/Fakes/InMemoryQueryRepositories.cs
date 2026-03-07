using LuSplit.Application.Commands;
using LuSplit.Application.Ports;
using LuSplit.Domain.Entities;

namespace LuSplit.Application.Tests.Fakes;

internal sealed class InMemoryQueryRepositories : IGroupRepository, IParticipantRepository, IEconomicUnitRepository, IExpenseRepository
{
    public List<Group> Groups { get; } = new();

    public List<Participant> Participants { get; } = new();

    public List<EconomicUnit> EconomicUnits { get; } = new();

    public List<Expense> Expenses { get; } = new();

    public Task<Group?> GetByIdAsync(string groupId, CancellationToken cancellationToken)
    {
        var group = Groups.FirstOrDefault(candidate => string.Equals(candidate.Id, groupId, StringComparison.Ordinal));
        return Task.FromResult(group);
    }

    public Task<IReadOnlyList<Participant>> ListParticipantsByGroupIdAsync(string groupId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Participant> participants = Participants
            .Where(candidate => string.Equals(candidate.GroupId, groupId, StringComparison.Ordinal))
            .ToArray();

        return Task.FromResult(participants);
    }

    public Task<IReadOnlyList<EconomicUnit>> ListEconomicUnitsByGroupIdAsync(string groupId, CancellationToken cancellationToken)
    {
        IReadOnlyList<EconomicUnit> economicUnits = EconomicUnits
            .Where(candidate => string.Equals(candidate.GroupId, groupId, StringComparison.Ordinal))
            .ToArray();

        return Task.FromResult(economicUnits);
    }

    public Task AddAsync(AddExpenseCommand command, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<IReadOnlyList<Expense>> ListExpensesByGroupIdAsync(string groupId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Expense> expenses = Expenses
            .Where(candidate => string.Equals(candidate.GroupId, groupId, StringComparison.Ordinal))
            .ToArray();

        return Task.FromResult(expenses);
    }

    public Task<Expense?> GetExpenseByIdAsync(string expenseId, CancellationToken cancellationToken)
    {
        var expense = Expenses.FirstOrDefault(candidate => string.Equals(candidate.Id, expenseId, StringComparison.Ordinal));
        return Task.FromResult(expense);
    }

    public Task SaveAsync(Expense expense, CancellationToken cancellationToken)
    {
        var existingIndex = Expenses.FindIndex(candidate => string.Equals(candidate.Id, expense.Id, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            Expenses[existingIndex] = expense;
        }
        else
        {
            Expenses.Add(expense);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string groupId, string expenseId, CancellationToken cancellationToken)
    {
        Expenses.RemoveAll(candidate =>
            string.Equals(candidate.GroupId, groupId, StringComparison.Ordinal)
            && string.Equals(candidate.Id, expenseId, StringComparison.Ordinal));

        return Task.CompletedTask;
    }
}
