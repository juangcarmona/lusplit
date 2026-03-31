using LuSplit.Application.Expenses.Ports;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Payments.Ports;
using LuSplit.Domain.Expenses;
using LuSplit.Domain.Groups;
using LuSplit.Domain.Payments;

namespace LuSplit.Application.Tests.Fakes;

internal sealed class InMemoryQueryRepositories : IGroupRepository, IParticipantRepository, IEconomicUnitRepository, IExpenseRepository, ITransferRepository
{
    public List<Group> Groups { get; } = new();

    public List<Participant> Participants { get; } = new();

    public List<EconomicUnit> EconomicUnits { get; } = new();

    public List<Expense> Expenses { get; } = new();

    public List<Transfer> Transfers { get; } = new();

    public Task<Group?> GetByIdAsync(string groupId, CancellationToken cancellationToken)
    {
        var group = Groups.FirstOrDefault(candidate => string.Equals(candidate.Id, groupId, StringComparison.Ordinal));
        return Task.FromResult(group);
    }

    public Task SaveGroupAsync(Group group, CancellationToken cancellationToken)
    {
        var existingIndex = Groups.FindIndex(candidate => string.Equals(candidate.Id, group.Id, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            Groups[existingIndex] = group;
        }
        else
        {
            Groups.Add(group);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Participant>> ListParticipantsByGroupIdAsync(string groupId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Participant> participants = Participants
            .Where(candidate => string.Equals(candidate.GroupId, groupId, StringComparison.Ordinal))
            .ToArray();

        return Task.FromResult(participants);
    }

    public Task SaveParticipantAsync(Participant participant, CancellationToken cancellationToken)
    {
        var existingIndex = Participants.FindIndex(candidate => string.Equals(candidate.Id, participant.Id, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            Participants[existingIndex] = participant;
        }
        else
        {
            Participants.Add(participant);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EconomicUnit>> ListEconomicUnitsByGroupIdAsync(string groupId, CancellationToken cancellationToken)
    {
        IReadOnlyList<EconomicUnit> economicUnits = EconomicUnits
            .Where(candidate => string.Equals(candidate.GroupId, groupId, StringComparison.Ordinal))
            .ToArray();

        return Task.FromResult(economicUnits);
    }

    public Task<EconomicUnit?> GetEconomicUnitByIdAsync(string economicUnitId, CancellationToken cancellationToken)
    {
        var economicUnit = EconomicUnits.FirstOrDefault(candidate => string.Equals(candidate.Id, economicUnitId, StringComparison.Ordinal));
        return Task.FromResult(economicUnit);
    }

    public Task SaveEconomicUnitAsync(EconomicUnit economicUnit, CancellationToken cancellationToken)
    {
        var existingIndex = EconomicUnits.FindIndex(candidate => string.Equals(candidate.Id, economicUnit.Id, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            EconomicUnits[existingIndex] = economicUnit;
        }
        else
        {
            EconomicUnits.Add(economicUnit);
        }

        return Task.CompletedTask;
    }

    public Task DeleteEconomicUnitAsync(string economicUnitId, CancellationToken cancellationToken)
    {
        EconomicUnits.RemoveAll(candidate => string.Equals(candidate.Id, economicUnitId, StringComparison.Ordinal));
        return Task.CompletedTask;
    }

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

    public Task<IReadOnlyList<Transfer>> ListTransfersByGroupIdAsync(string groupId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Transfer> transfers = Transfers
            .Where(candidate => string.Equals(candidate.GroupId, groupId, StringComparison.Ordinal))
            .ToArray();

        return Task.FromResult(transfers);
    }

    public Task SaveTransferAsync(Transfer transfer, CancellationToken cancellationToken)
    {
        var existingIndex = Transfers.FindIndex(candidate => string.Equals(candidate.Id, transfer.Id, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            Transfers[existingIndex] = transfer;
        }
        else
        {
            Transfers.Add(transfer);
        }

        return Task.CompletedTask;
    }
}
