using LuSplit.Application.Models;
using LuSplit.Domain.Split;

namespace LuSplit.App.Services;

public interface IExpenseDetailsDataService
{
    Task<GroupOverviewModel> GetOverviewAsync();
    Task<ExpenseModel?> GetExpenseAsync(string expenseId);
    Task DeleteExpenseAsync(string expenseId);
    Task UpdateExpenseAsync(
        string expenseId,
        string title,
        string paidByParticipantId,
        long amountMinor,
        DateTime date,
        SplitDefinition splitDefinition,
        string? notes);
}
