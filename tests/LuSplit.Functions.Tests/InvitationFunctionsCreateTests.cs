using Azure.Data.Tables;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Functions;
using LuSplit.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
namespace LuSplit.Functions.Tests;

public sealed class InvitationFunctionsCreateTests
{
    [Fact]
    public async Task CreateInvitation_GroupNotFound_Returns404()
    {
        var (functions, _, groupStore) = Build();
        groupStore.GetGroupAsync("g1", Arg.Any<CancellationToken>()).Returns((TableEntity?)null);

        var req = MockRequest(new CreateInvitationRequest("g1", "user-1", "dev-1"));
        var result = await functions.CreateInvitation(req, "g1", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CreateInvitation_NonOwner_Returns403()
    {
        var (functions, _, groupStore) = Build();
        groupStore.GetGroupAsync("g1", Arg.Any<CancellationToken>())
            .Returns(GroupEntity("owner-1"));

        var req = MockRequest(new CreateInvitationRequest("g1", "other-user", "dev-1"));
        var result = await functions.CreateInvitation(req, "g1", CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, obj.StatusCode);
    }

    [Fact]
    public async Task CreateInvitation_Owner_Returns201WithToken()
    {
        var (functions, _, groupStore) = Build();
        groupStore.GetGroupAsync("g1", Arg.Any<CancellationToken>())
            .Returns(GroupEntity("owner-1"));

        var req = MockRequest(new CreateInvitationRequest("g1", "owner-1", "dev-1"));
        var result = await functions.CreateInvitation(req, "g1", CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, obj.StatusCode);
        var response = Assert.IsType<CreateInvitationResponse>(obj.Value);
        Assert.NotEmpty(response.InvitationCode);
        Assert.True(response.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CreateInvitation_Owner_SavesInvitationToStore()
    {
        var (functions, invitationStore, groupStore) = Build();
        groupStore.GetGroupAsync("g1", Arg.Any<CancellationToken>())
            .Returns(GroupEntity("owner-1"));

        var req = MockRequest(new CreateInvitationRequest("g1", "owner-1", "dev-1"));
        await functions.CreateInvitation(req, "g1", CancellationToken.None);

        await invitationStore.Received(1).SaveInvitationAsync(
            Arg.Any<string>(), "g1", "owner-1",
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelInvitation_InvitationNotFound_Returns404()
    {
        var (functions, invitationStore, _) = Build();
        invitationStore.GetInvitationAsync("g1", "inv-999", Arg.Any<CancellationToken>())
            .Returns((TableEntity?)null);

        var req = Substitute.For<HttpRequest>();
        var result = await functions.CancelInvitation(req, "g1", "inv-999", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private static (InvitationFunctions functions, IInvitationStore invitationStore, IGroupMetadataStore groupStore) Build()
    {
        var invitationStore = Substitute.For<IInvitationStore>();
        var groupStore = Substitute.For<IGroupMetadataStore>();
        var logger = Substitute.For<ILogger<InvitationFunctions>>();

        invitationStore.EnsureTableExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        invitationStore.SaveInvitationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        return (new InvitationFunctions(invitationStore, groupStore, logger), invitationStore, groupStore);
    }

    private static HttpRequest MockRequest<T>(T body)
    {
        var context = new DefaultHttpContext();
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(body);
        context.Request.Body = new MemoryStream(json);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = json.Length;
        return context.Request;
    }

    private static TableEntity GroupEntity(string ownerId)
    {
        var entity = new TableEntity("groups", "g1") { ["OwnerId"] = ownerId };
        return entity;
    }
}

