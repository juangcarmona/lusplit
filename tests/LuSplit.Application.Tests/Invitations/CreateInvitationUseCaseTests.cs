using LuSplit.Application.Groups.Ports;
using LuSplit.Application.Invitations.Ports;
using LuSplit.Application.Invitations.UseCases;
using LuSplit.Application.Shared.Errors;
using LuSplit.Application.Shared.Ports;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Domain.Groups;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LuSplit.Application.Tests.Invitations;

public sealed class CreateInvitationUseCaseTests
{
    private const string GroupId = "group-1";
    private const string OwnerId = "owner-1";
    private const string DeviceId = "device-1";

    private static (CreateInvitationUseCase useCase, ISharedGroupStateRepository stateRepo, IInvitationPort port, IAuthPort auth)
        BuildWith(SharedGroupState? sharedState = null, string? currentUserId = OwnerId)
    {
        var stateRepo = Substitute.For<ISharedGroupStateRepository>();
        var port = Substitute.For<IInvitationPort>();
        var auth = Substitute.For<IAuthPort>();
        var groupRepo = Substitute.For<IGroupRepository>();

        auth.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(currentUserId);

        stateRepo.GetByGroupIdAsync(GroupId, Arg.Any<CancellationToken>())
            .Returns(sharedState ?? new SharedGroupState(true, "container-1", OwnerId, 1, SyncStatus.UpToDate, false));

        port.CreateInvitationAsync(Arg.Any<CreateInvitationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateInvitationResponse("inv-1", "tok-abc", DateTimeOffset.UtcNow.AddDays(7)));

        var useCase = new CreateInvitationUseCase(stateRepo, port, auth, groupRepo);
        return (useCase, stateRepo, port, auth);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_ReturnInvitationResponse()
    {
        var (useCase, _, port, _) = BuildWith();

        var result = await useCase.ExecuteAsync(GroupId, DeviceId);

        Assert.Equal("inv-1", result.InvitationId);
        Assert.Equal("tok-abc", result.InvitationCode);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_CallsInvitationPortOnce()
    {
        var (useCase, _, port, _) = BuildWith();

        await useCase.ExecuteAsync(GroupId, DeviceId);

        await port.Received(1).CreateInvitationAsync(
            Arg.Is<CreateInvitationRequest>(r => r.GroupId == GroupId && r.InvitedByUserId == OwnerId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NotSignedIn_ThrowsValidationError()
    {
        var (useCase, _, _, _) = BuildWith(currentUserId: null);

        await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(GroupId, DeviceId));
    }

    [Fact]
    public async Task ExecuteAsync_NonSharedGroup_ThrowsValidationError()
    {
        var nonSharedState = new SharedGroupState(false, null, OwnerId, 1, SyncStatus.UpToDate, false);
        var (useCase, _, _, _) = BuildWith(sharedState: nonSharedState);

        await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(GroupId, DeviceId));
    }

    [Fact]
    public async Task ExecuteAsync_NonOwner_ThrowsValidationError()
    {
        var (useCase, _, _, _) = BuildWith(currentUserId: "other-user");

        await Assert.ThrowsAsync<ValidationError>(() => useCase.ExecuteAsync(GroupId, DeviceId));
    }

    [Fact]
    public async Task ExecuteAsync_ExpiryIsInFuture()
    {
        var (useCase, _, _, _) = BuildWith();

        var result = await useCase.ExecuteAsync(GroupId, DeviceId);

        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);
    }
}
