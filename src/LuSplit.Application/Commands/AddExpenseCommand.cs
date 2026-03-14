using LuSplit.Domain.Money;

namespace LuSplit.Application.Commands;

public sealed record AddExpenseCommand(string GroupId, string PaidByParticipantId, MoneyAmount Amount, DateOnly Date)
{
    public static AddExpenseCommand Create(string groupId, string paidByParticipantId, decimal amountMinorUnits, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            throw new ArgumentException("Group ID is required.", nameof(groupId));
        }

        if (string.IsNullOrWhiteSpace(paidByParticipantId))
        {
            throw new ArgumentException("Payer participant ID is required.", nameof(paidByParticipantId));
        }

        return new AddExpenseCommand(
            groupId,
            paidByParticipantId,
            MoneyAmount.FromMinorUnitsDecimal(amountMinorUnits),
            date);
    }
}
