using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Invitations.Ports;
using LuSplit.Application.Invitations.Queries;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;
using NSubstitute;

namespace LuSplit.Application.Tests.Invitations;

public sealed class GetPendingInvitationsQueryTests
{
    private const string GroupId = "group-1";
    private const string OwnerId = "owner-1";
    private const string MemberId = "member-1";

    private readonly IInvitationPort _invitationPort = Substitute.For<IInvitationPort>();
    private readonly ISharedGroupStateRepository _sharedStateRepo = Substitute.For<ISharedGroupStateRepository>();

    private GetPendingInvitationsQuery CreateSut() => new(_invitationPort, _sharedStateRepo);

    private void SetupOwner()
    {
        var state = new SharedGroupState(
            IsShared: true,
            RemoteContainerName: "container-1",
            OwnerId: OwnerId,
            CurrentKeyVersion: 1,
            SyncStatus: SyncStatus.UpToDate,
            IsReadOnly: false);
        _sharedStateRepo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>()).Returns(state);
    }

    [Fact]
    public async Task ExecuteAsync_OwnerGetsInvitations()
    {
        SetupOwner();
        var pending = new PendingInvitationDto("inv-1", "code-1", DateTimeOffset.UtcNow.AddDays(1), "Pending");
        _invitationPort.ListPendingInvitationsAsync(GroupId, OwnerId, Arg.Any<CancellationToken>())
            .Returns(new ListPendingInvitationsResponse([pending]));

        var result = await CreateSut().ExecuteAsync(GroupId, OwnerId);

        Assert.Single(result);
        Assert.Equal("inv-1", result[0].InvitationId);
    }

    [Fact]
    public async Task ExecuteAsync_NonOwner_ReturnsEmptyList()
    {
        SetupOwner();

        var result = await CreateSut().ExecuteAsync(GroupId, MemberId);

        Assert.Empty(result);
        await _invitationPort.DidNotReceive().ListPendingInvitationsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NonSharedGroup_ReturnsEmptyList()
    {
        _sharedStateRepo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns((SharedGroupState?)null);

        var result = await CreateSut().ExecuteAsync(GroupId, OwnerId);

        Assert.Empty(result);
    }
}
