using LuSplit.Application.Expenses.Models;
using LuSplit.Application.Groups.Models;
using LuSplit.Domain.Expenses;

namespace LuSplit.App.Features.Expenses.ExpenseDetails;

public interface IExpenseDetailsDataService
{
    Task<GroupOverviewModel> GetOverviewAsync();
    Task<GroupOverviewModel> GetOverviewAsync(string groupId);
    Task<ExpenseModel?> GetExpenseAsync(string expenseId);
    Task<ExpenseModel?> GetExpenseAsync(string expenseId, string groupId);
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
