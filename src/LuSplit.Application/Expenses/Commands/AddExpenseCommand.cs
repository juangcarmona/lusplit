using LuSplit.Application.Shared.Commands;
using LuSplit.Domain.Shared;

namespace LuSplit.Application.Expenses.Commands;

public sealed record AddExpenseCommand(string GroupId, string PaidByParticipantId, MoneyAmount Amount, DateOnly Date)
{
    public static AddExpenseCommand Create(string groupId, string paidByParticipantId, decimal amountMinorUnits, DateOnly date)
    {
        UseCaseGuards.AssertNonEmpty(groupId, "groupId");
        UseCaseGuards.AssertNonEmpty(paidByParticipantId, "paidByParticipantId");

        return new AddExpenseCommand(
            groupId,
            paidByParticipantId,
            MoneyAmount.FromMinorUnitsDecimal(amountMinorUnits),
            date);
    }
}
