using LuSplit.Infrastructure.Snapshot;

namespace LuSplit.Infrastructure.Tests;

public sealed class SnapshotContractTests
{
    [Fact]
    public void SnapshotVersionRemainsV1ForParity()
    {
        Assert.Equal(1, SnapshotContract.Version);
    }
}
