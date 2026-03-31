using LuSplit.App.Services.Persistence;
using LuSplit.Application.Groups.Models;
using LuSplit.Domain.Expenses;

namespace LuSplit.App.Features.Expenses.AddExpense;

public interface IAddExpenseDataService
{
    Task<GroupOverviewModel> GetOverviewAsync();
    EventDraftDefaults GetEventDraftDefaults();
    Task AddExpenseAsync(
        string title,
        long amountMinor,
        string paidByParticipantId,
        DateTime date,
        IReadOnlyList<string> participantIds,
        string? icon,
        SplitDefinition? splitDefinition = null);
}
