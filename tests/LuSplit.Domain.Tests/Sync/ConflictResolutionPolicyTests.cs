using LuSplit.Domain.Sync;

namespace LuSplit.Domain.Tests.Sync;

public sealed class ConflictResolutionPolicyTests
{
    // --- LWW by HLC ---

    [Fact]
    public void EditVsEdit_HigherHlcWins()
    {
        var earlier = MakeOperation("op-a", OperationType.EditExpense, "expense-1", "2024-01-01T00:00:00Z~0001~dev-a");
        var later   = MakeOperation("op-b", OperationType.EditExpense, "expense-1", "2024-01-01T00:00:01Z~0001~dev-b");

        var result = ConflictResolutionPolicy.Resolve(earlier, later);

        Assert.Equal(ConflictOutcome.LaterWins, result.Outcome);
        Assert.Equal("op-b", result.WinningOperationId);
        Assert.Equal("op-a", result.LosingOperationId);
    }

    [Fact]
    public void EditVsEdit_EarlierFirst_SameResult()
    {
        var earlier = MakeOperation("op-a", OperationType.EditExpense, "expense-1", "2024-01-01T00:00:00Z~0001~dev-a");
        var later   = MakeOperation("op-b", OperationType.EditExpense, "expense-1", "2024-01-01T00:00:01Z~0001~dev-b");

        var result = ConflictResolutionPolicy.Resolve(later, earlier);

        Assert.Equal(ConflictOutcome.LaterWins, result.Outcome);
        Assert.Equal("op-b", result.WinningOperationId);
    }

    // --- Delete wins ---

    [Fact]
    public void DeleteVsEdit_DeleteWinsRegardlessOfTimestamp()
    {
        var edit   = MakeOperation("op-edit",   OperationType.EditExpense,   "expense-1", "2024-01-02T00:00:00Z~0001~dev-a");
        var delete = MakeOperation("op-delete", OperationType.DeleteExpense, "expense-1", "2024-01-01T00:00:00Z~0001~dev-b");

        var result = ConflictResolutionPolicy.Resolve(edit, delete);

        Assert.Equal(ConflictOutcome.DeleteWins, result.Outcome);
        Assert.Equal("op-delete", result.WinningOperationId);
    }

    [Fact]
    public void EditVsDelete_DeleteWins()
    {
        var edit   = MakeOperation("op-edit",   OperationType.EditExpense,   "expense-1", "2024-01-01T00:00:00Z~0001~dev-a");
        var delete = MakeOperation("op-delete", OperationType.DeleteExpense, "expense-1", "2024-01-02T00:00:00Z~0001~dev-b");

        var result = ConflictResolutionPolicy.Resolve(delete, edit);

        Assert.Equal(ConflictOutcome.DeleteWins, result.Outcome);
        Assert.Equal("op-delete", result.WinningOperationId);
    }

    // --- Additions are commutative (no conflict) ---

    [Fact]
    public void AddVsAdd_DifferentEntities_NoConflict()
    {
        var add1 = MakeOperation("op-a", OperationType.AddExpense, "expense-1", "2024-01-01T00:00:00Z~0001~dev-a");
        var add2 = MakeOperation("op-b", OperationType.AddExpense, "expense-2", "2024-01-01T00:00:00Z~0001~dev-b");

        var conflict = ConflictResolutionPolicy.IsConflict(add1, add2);

        Assert.False(conflict);
    }

    [Fact]
    public void NoConflict_WhenEntitiesAreDifferent()
    {
        var a = MakeOperation("op-a", OperationType.EditExpense, "expense-1", "2024-01-01T00:00:00Z~0001~dev-a");
        var b = MakeOperation("op-b", OperationType.EditExpense, "expense-2", "2024-01-01T00:00:00Z~0001~dev-b");

        var conflict = ConflictResolutionPolicy.IsConflict(a, b);

        Assert.False(conflict);
    }

    private static Operation MakeOperation(string id, OperationType type, string entityId, string hlc) =>
        new(id, "group-1", "dev-x", "user-x", hlc, type, entityId, [], 1, DateTimeOffset.UtcNow);
}
