using LuSplit.Application.Expenses.Ports;
using LuSplit.Application.Expenses.Models;
using LuSplit.Application.Groups.Models;
using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Shared.Commands;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Shared.Ports;
using LuSplit.Domain.Expenses;

namespace LuSplit.Application.Expenses.Commands;

public sealed class AddExpenseUseCase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IParticipantRepository _participantRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;

    public AddExpenseUseCase(
        IGroupRepository groupRepository,
        IParticipantRepository participantRepository,
        IExpenseRepository expenseRepository,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _groupRepository = groupRepository;
        _participantRepository = participantRepository;
        _expenseRepository = expenseRepository;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<ExpenseModel> ExecuteAsync(AddExpenseInput input, CancellationToken cancellationToken = default)
    {
        UseCaseGuards.AssertNonEmpty(input.GroupId, "groupId");
        UseCaseGuards.AssertNonEmpty(input.Title, "title");
        UseCaseGuards.AssertNonEmpty(input.PaidByParticipantId, "paidByParticipantId");

        if (input.AmountMinor <= 0)
        {
            throw new ValidationError("amountMinor must be greater than zero");
        }

        var group = await _groupRepository.GetByIdAsync(input.GroupId, cancellationToken);
        if (group is null)
        {
            throw new NotFoundError($"Group not found: {input.GroupId}");
        }

        if (group.Closed)
        {
            throw new ValidationError($"Group is closed: {group.Id}");
        }

        var participants = await _participantRepository.ListParticipantsByGroupIdAsync(input.GroupId, cancellationToken);
        var payerExists = participants.Any(p => string.Equals(p.Id, input.PaidByParticipantId, StringComparison.Ordinal));
        if (!payerExists)
        {
            throw new ValidationError($"Payer is not in group {input.GroupId}");
        }

        var date = UseCaseGuards.ResolveDate(input.Date, _clock.NowIso());

        var expense = new Expense(
            _idGenerator.NextId(),
            input.GroupId,
            input.Title,
            input.PaidByParticipantId,
            input.AmountMinor,
            date,
            input.SplitDefinition,
            input.Notes);

        _ = SplitEvaluator.EvaluateSplit(expense, participants);
        await _expenseRepository.SaveAsync(expense, cancellationToken);

        return new ExpenseModel(
            expense.Id,
            expense.GroupId,
            expense.Title,
            expense.PaidByParticipantId,
            expense.AmountMinor,
            expense.Date,
            expense.SplitDefinition,
            expense.Notes);
    }

}
