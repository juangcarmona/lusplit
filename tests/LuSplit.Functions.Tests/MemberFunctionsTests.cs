using Azure.Data.Tables;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Functions;
using LuSplit.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LuSplit.Functions.Tests;

public sealed class MemberFunctionsTests
{
    private const string GroupId = "group-1";
    private const string OwnerId = "user-owner";
    private const string MemberId = "user-member";
    private const string NonOwnerId = "user-other";

    private static (MemberFunctions functions, IGroupMetadataStore groupStore) Build()
    {
        var groupStore = Substitute.For<IGroupMetadataStore>();
        var logger = Substitute.For<ILogger<MemberFunctions>>();

        groupStore.SetKeyRotationRequiredAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        groupStore.UpdateOwnerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return (new MemberFunctions(groupStore, logger), groupStore);
    }

    private static TableEntity GroupEntity(string ownerId = OwnerId) =>
        new("groups", GroupId) { ["OwnerId"] = ownerId };

    private static HttpRequest RevokeRequest(string revokedByUserId, string memberUserId = MemberId)
    {
        var body = new RevokeMemberRequest(memberUserId, revokedByUserId);
        var context = new DefaultHttpContext();
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(body);
        context.Request.Body = new MemoryStream(json);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = json.Length;
        return context.Request;
    }

    private static HttpRequest TransferRequest(string newOwner, string caller)
    {
        var body = new TransferOwnershipRequest(newOwner, caller);
        var context = new DefaultHttpContext();
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(body);
        context.Request.Body = new MemoryStream(json);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = json.Length;
        return context.Request;
    }

    [Fact]
    public async Task RevokeMember_GroupNotFound_Returns404()
    {
        var (sut, store) = Build();
        store.GetGroupAsync(GroupId, Arg.Any<CancellationToken>()).Returns((TableEntity?)null);

        var result = await sut.RevokeMember(RevokeRequest(OwnerId), GroupId, MemberId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RevokeMember_NonOwnerCaller_Returns403()
    {
        var (sut, store) = Build();
        store.GetGroupAsync(GroupId, Arg.Any<CancellationToken>()).Returns(GroupEntity());

        var result = await sut.RevokeMember(RevokeRequest(NonOwnerId), GroupId, MemberId, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task RevokeMember_OwnerRevokesThemselves_Returns400()
    {
        var (sut, store) = Build();
        store.GetGroupAsync(GroupId, Arg.Any<CancellationToken>()).Returns(GroupEntity());

        var result = await sut.RevokeMember(RevokeRequest(OwnerId), GroupId, OwnerId, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RevokeMember_Valid_Returns204()
    {
        var (sut, store) = Build();
        store.GetGroupAsync(GroupId, Arg.Any<CancellationToken>()).Returns(GroupEntity());

        var result = await sut.RevokeMember(RevokeRequest(OwnerId), GroupId, MemberId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task TransferOwnership_NonOwnerCaller_Returns403()
    {
        var (sut, store) = Build();
        store.GetGroupAsync(GroupId, Arg.Any<CancellationToken>()).Returns(GroupEntity());

        var result = await sut.TransferOwnership(TransferRequest(MemberId, NonOwnerId), GroupId, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
    }

    [Fact]
    public async Task TransferOwnership_SameUser_Returns400()
    {
        var (sut, store) = Build();
        store.GetGroupAsync(GroupId, Arg.Any<CancellationToken>()).Returns(GroupEntity());

        var result = await sut.TransferOwnership(TransferRequest(OwnerId, OwnerId), GroupId, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task TransferOwnership_Valid_Returns204()
    {
        var (sut, store) = Build();
        store.GetGroupAsync(GroupId, Arg.Any<CancellationToken>()).Returns(GroupEntity());

        var result = await sut.TransferOwnership(TransferRequest(MemberId, OwnerId), GroupId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }
}
