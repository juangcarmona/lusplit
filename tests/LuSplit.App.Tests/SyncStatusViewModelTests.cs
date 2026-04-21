using LuSplit.App.Features.Sync;
using LuSplit.Domain.Groups;

namespace LuSplit.App.Tests;

public sealed class SyncStatusViewModelTests
{
    [Fact]
    public void WhenSyncStatusIsNull_IsVisible_IsFalse()
    {
        var vm = new SyncStatusViewModel { SyncStatus = null };

        Assert.False(vm.IsVisible);
    }

    [Fact]
    public void WhenSyncStatusIsSet_IsVisible_IsTrue()
    {
        var vm = new SyncStatusViewModel { SyncStatus = SyncStatus.UpToDate };

        Assert.True(vm.IsVisible);
    }

    [Theory]
    [InlineData(SyncStatus.UpToDate, "Up to date")]
    [InlineData(SyncStatus.Syncing, "Syncing\u2026")]
    [InlineData(SyncStatus.PendingLocalChanges, "Will update when online")]
    [InlineData(SyncStatus.SyncError, "Sync error")]
    public void StatusText_ReturnsCorrectTextPerState(SyncStatus status, string expectedText)
    {
        var vm = new SyncStatusViewModel { SyncStatus = status };

        Assert.Equal(expectedText, vm.StatusText);
    }

    [Fact]
    public void SyncStatus_Change_RaisesPropertyChanged()
    {
        var vm = new SyncStatusViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.SyncStatus = SyncStatus.Syncing;

        Assert.Contains(nameof(vm.StatusText), changed);
        Assert.Contains(nameof(vm.StatusIconGlyph), changed);
        Assert.Contains(nameof(vm.IsVisible), changed);
    }
}
