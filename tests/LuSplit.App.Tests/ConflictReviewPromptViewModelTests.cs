using LuSplit.App.Features.Expenses;
using LuSplit.App.Services;

namespace LuSplit.App.Tests;

public sealed class ConflictReviewPromptViewModelTests
{
    [Fact]
    public void IsVisible_WhenConflictFlagNotSet_IsFalse()
    {
        var store = new ConflictFlagStore();
        var vm = new ConflictReviewPromptViewModel(store);

        vm.Load("expense-1");

        Assert.False(vm.IsVisible);
        Assert.False(vm.HasConflict);
    }

    [Fact]
    public void IsVisible_WhenConflictFlagSet_IsTrue()
    {
        var store = new ConflictFlagStore();
        store.Set("expense-1");

        var vm = new ConflictReviewPromptViewModel(store);
        vm.Load("expense-1");

        Assert.True(vm.IsVisible);
        Assert.True(vm.HasConflict);
    }

    [Fact]
    public void Dismiss_ClearsConflictFlag()
    {
        var store = new ConflictFlagStore();
        store.Set("expense-1");

        var vm = new ConflictReviewPromptViewModel(store);
        vm.Load("expense-1");
        vm.DismissCommand.Execute(null);

        Assert.False(vm.HasConflict);
        Assert.False(vm.IsVisible);
        Assert.False(store.IsSet("expense-1"));
    }

    [Fact]
    public void PropertyChanged_RaisedForIsVisible_WhenHasConflictChanges()
    {
        var store = new ConflictFlagStore();
        store.Set("expense-1");

        var vm = new ConflictReviewPromptViewModel(store);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Load("expense-1");
        vm.DismissCommand.Execute(null);

        Assert.Contains(nameof(vm.HasConflict), raised);
        Assert.Contains(nameof(vm.IsVisible), raised);
    }
}
