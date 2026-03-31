using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Expenses.Ports;
using LuSplit.Application.Expenses.Models;
using LuSplit.Application.Groups.Models;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Commands;
using LuSplit.Domain.Expenses;

namespace LuSplit.Application.Expenses.Commands;

public sealed class EditExpenseUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IParticipantRepository _participantRepository;
    private readonly IExpenseRepository _expenseRepository;

    public EditExpenseUseCase(
        IGroupRepository groupRepository,
        IParticipantRepository participantRepository,
        IExpenseRepository expenseRepository)
    {
        _groupRepository = groupRepository;
        _participantRepository = participantRepository;
        _expenseRepository = expenseRepository;
    }

    public async Task<ExpenseModel> ExecuteAsync(EditExpenseInput input, CancellationToken cancellationToken = default)
    {
        UseCaseGuards.AssertNonEmpty(input.GroupId, "groupId");
        UseCaseGuards.AssertNonEmpty(input.ExpenseId, "expenseId");

        var group = await _groupRepository.GetByIdAsync(input.GroupId, cancellationToken);
        if (group is null)
        {
            throw new NotFoundError($"Group not found: {input.GroupId}");
        }

        if (group.Closed)
        {
            throw new ValidationError($"Group is closed: {group.Id}");
        }

        var existing = await _expenseRepository.GetExpenseByIdAsync(input.ExpenseId, cancellationToken);
        if (existing is null || !string.Equals(existing.GroupId, input.GroupId, StringComparison.Ordinal))
        {
            throw new NotFoundError($"Expense not found: {input.ExpenseId}");
        }

        if (input.AmountMinor is <= 0)
        {
            throw new ValidationError("amountMinor must be greater than zero");
        }

        var nextDate = UseCaseGuards.ResolveDate(input.Date, existing.Date);

        var nextExpense = existing with
        {
            Title = input.Title ?? existing.Title,
            PaidByParticipantId = input.PaidByParticipantId ?? existing.PaidByParticipantId,
            AmountMinor = input.AmountMinor ?? existing.AmountMinor,
            SplitDefinition = input.SplitDefinition ?? existing.SplitDefinition,
            Date = nextDate,
            Notes = input.Notes ?? existing.Notes
        };

        var participants = await _participantRepository.ListParticipantsByGroupIdAsync(input.GroupId, cancellationToken);
        var payerExists = participants.Any(p => string.Equals(p.Id, nextExpense.PaidByParticipantId, StringComparison.Ordinal));
        if (!payerExists)
        {
            throw new ValidationError($"Payer is not in group {input.GroupId}");
        }

        _ = SplitEvaluator.EvaluateSplit(nextExpense, participants);
        await _expenseRepository.SaveAsync(nextExpense, cancellationToken);

        return new ExpenseModel(
            nextExpense.Id,
            nextExpense.GroupId,
            nextExpense.Title,
            nextExpense.PaidByParticipantId,
            nextExpense.AmountMinor,
            nextExpense.Date,
            nextExpense.SplitDefinition,
            nextExpense.Notes);
    }

}
