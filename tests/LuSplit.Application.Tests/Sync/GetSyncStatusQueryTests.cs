using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Sync.Queries;
using LuSplit.Domain.Groups;
using NSubstitute;

namespace LuSplit.Application.Tests.Sync;

public sealed class GetSyncStatusQueryTests
{
    private const string GroupId = "group-1";

    private readonly ISharedGroupStateRepository _repo = Substitute.For<ISharedGroupStateRepository>();

    private GetSyncStatusQuery CreateSut() => new(_repo);

    private void SetupState(bool isShared, SyncStatus status = SyncStatus.UpToDate)
    {
        var state = new SharedGroupState(
            IsShared: isShared,
            RemoteContainerName: "container-1",
            OwnerId: "owner-1",
            CurrentKeyVersion: 1,
            SyncStatus: status,
            IsReadOnly: false);
        _repo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>()).Returns(state);
    }

    [Fact]
    public async Task ExecuteAsync_SharedGroupUpToDate_ReturnsUpToDate()
    {
        SetupState(isShared: true, SyncStatus.UpToDate);

        var result = await CreateSut().ExecuteAsync(GroupId);

        Assert.Equal(SyncStatus.UpToDate, result);
    }

    [Fact]
    public async Task ExecuteAsync_SharedGroupSyncing_ReturnsSyncing()
    {
        SetupState(isShared: true, SyncStatus.Syncing);

        var result = await CreateSut().ExecuteAsync(GroupId);

        Assert.Equal(SyncStatus.Syncing, result);
    }

    [Fact]
    public async Task ExecuteAsync_SharedGroupSyncError_ReturnsSyncError()
    {
        SetupState(isShared: true, SyncStatus.SyncError);

        var result = await CreateSut().ExecuteAsync(GroupId);

        Assert.Equal(SyncStatus.SyncError, result);
    }

    [Fact]
    public async Task ExecuteAsync_LocalOnlyGroup_ReturnsNull()
    {
        _repo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>()).Returns((SharedGroupState?)null);

        var result = await CreateSut().ExecuteAsync(GroupId);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_NonSharedGroup_ReturnsNull()
    {
        SetupState(isShared: false);

        var result = await CreateSut().ExecuteAsync(GroupId);

        Assert.Null(result);
    }
}
