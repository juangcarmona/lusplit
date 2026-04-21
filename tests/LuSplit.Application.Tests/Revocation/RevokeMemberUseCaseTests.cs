using LuSplit.Application.Revocation.Ports;
using LuSplit.Application.Revocation.UseCases;
using LuSplit.Application.Shared.Ports;
using LuSplit.Domain.Activity;
using LuSplit.Domain.Groups;
using LuSplit.Application.Groups.Ports;
using NSubstitute;

namespace LuSplit.Application.Tests.Revocation;

public sealed class RevokeMemberUseCaseTests
{
    private const string GroupId = "group-1";
    private const string OwnerId = "user-owner";
    private const string MemberId = "user-member";

    private readonly IRevocationPort _revocationPort = Substitute.For<IRevocationPort>();
    private readonly ISharedGroupStateRepository _sharedStateRepo = Substitute.For<ISharedGroupStateRepository>();
    private readonly IActivityEntryPort _activityEntryPort = Substitute.For<IActivityEntryPort>();
    private readonly IIdGenerator _idGenerator = Substitute.For<IIdGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private RevokeMemberUseCase CreateSut() => new(
        _revocationPort, _sharedStateRepo, _activityEntryPort, _idGenerator, _clock);

    private void SetupSharedGroup()
    {
        var state = new SharedGroupState(
            IsShared: true,
            RemoteContainerName: "container-1",
            OwnerId: OwnerId,
            CurrentKeyVersion: 1,
            SyncStatus: SyncStatus.UpToDate,
            IsReadOnly: false);
        _sharedStateRepo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>()).Returns(state);
        _idGenerator.NextId().Returns("entry-id-1");
        _clock.UtcNow.Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRevoke_CallsRevocationPort()
    {
        SetupSharedGroup();

        await CreateSut().ExecuteAsync(GroupId, MemberId, OwnerId);

        await _revocationPort.Received(1).RevokeMemberAsync(GroupId, MemberId, OwnerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ValidRevoke_WritesActivityEntry()
    {
        SetupSharedGroup();

        await CreateSut().ExecuteAsync(GroupId, MemberId, OwnerId);

        await _activityEntryPort.Received(1).InsertAsync(
            Arg.Is<ActivityEntry>(e => e.EntryType == ActivityEntryType.MemberRevoked && e.EntityId == MemberId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NonOwnerCaller_ThrowsUnauthorized()
    {
        SetupSharedGroup();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CreateSut().ExecuteAsync(GroupId, MemberId, "non-owner-user"));
    }

    [Fact]
    public async Task ExecuteAsync_OwnerRevokesThemselves_ThrowsInvalidOperation()
    {
        SetupSharedGroup();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateSut().ExecuteAsync(GroupId, OwnerId, OwnerId));
    }

    [Fact]
    public async Task ExecuteAsync_NonSharedGroup_ThrowsInvalidOperation()
    {
        _sharedStateRepo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns((SharedGroupState?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateSut().ExecuteAsync(GroupId, MemberId, OwnerId));
    }
}
