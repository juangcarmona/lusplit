using LuSplit.Application.Expenses.Commands;
using LuSplit.Application.Shared.Errors;
using LuSplit.Domain.Shared;

namespace LuSplit.Application.Tests;

public sealed class AddExpenseCommandTests
{
    [Fact]
    public void CreateRejectsFractionalMinorUnits()
    {
        Assert.Throws<DomainInvariantException>(() =>
            AddExpenseCommand.Create("g1", "p1", 100.2m, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void CreateRejectsMissingGroupId()
    {
        Assert.Throws<ValidationError>(() =>
            AddExpenseCommand.Create(" ", "p1", 100m, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void CreateProducesMinorUnitAmount()
    {
        var command = AddExpenseCommand.Create("g1", "p1", 101m, new DateOnly(2026, 1, 1));

        Assert.Equal(101, command.Amount.MinorUnits);
    }
}
