using LuSplit.Application.Errors;
using LuSplit.Application.Models;
using LuSplit.Application.Ports;
using LuSplit.Domain.Entities;
using LuSplit.Domain.Split;

namespace LuSplit.Application.Commands;

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
        AssertNonEmpty(input.GroupId, "groupId");
        AssertNonEmpty(input.ExpenseId, "expenseId");

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

        var nextDate = input.Date ?? existing.Date;
        if (!DateTimeOffset.TryParse(nextDate, out _))
        {
            throw new ValidationError("date must be a valid ISO date");
        }

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

    private static void AssertNonEmpty(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationError($"{fieldName} is required");
        }
    }
}
