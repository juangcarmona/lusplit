using LuSplit.Application.Models;
using LuSplit.Domain.Split;

namespace LuSplit.App.Services;

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
