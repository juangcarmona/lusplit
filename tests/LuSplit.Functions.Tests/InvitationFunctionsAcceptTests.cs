using Azure.Data.Tables;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Functions;
using LuSplit.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LuSplit.Functions.Tests;

public sealed class InvitationFunctionsAcceptTests
{
    private const string GroupId = "g1";
    private const string InvitationId = "inv-1";
    private const string ValidToken = "valid-token-abc";

    private static (InvitationFunctions functions, IInvitationStore invitationStore, IGroupMetadataStore groupStore) Build()
    {
        var invitationStore = Substitute.For<IInvitationStore>();
        var groupStore = Substitute.For<IGroupMetadataStore>();
        var logger = Substitute.For<ILogger<InvitationFunctions>>();

        invitationStore.EnsureTableExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        invitationStore.UpdateStatusAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return (new InvitationFunctions(invitationStore, groupStore, logger), invitationStore, groupStore);
    }

    private static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        return Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    private static TableEntity InvitationEntity(string status, DateTimeOffset? expiresAt = null)
    {
        var tokenHash = HashToken(ValidToken);
        var entity = new TableEntity(GroupId, InvitationId)
        {
            ["Status"] = status,
            ["ExpiresAt"] = expiresAt ?? DateTimeOffset.UtcNow.AddDays(7),
            ["InvitedByUserId"] = "user-owner",
            ["InvitedByDeviceId"] = "dev-owner",
            ["TokenHash"] = tokenHash
        };
        return entity;
    }

    private static TableEntity GroupEntity(string containerId = "container-1")
    {
        var entity = new TableEntity("groups", GroupId)
        {
            ["OwnerId"] = "user-owner",
            ["GroupName"] = "Test Group",
            ["ContainerName"] = containerId
        };
        return entity;
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

    [Fact]
    public async Task AcceptInvitation_TokenNotFound_Returns404()
    {
        var (functions, invitationStore, _) = Build();
        invitationStore.GetInvitationByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TableEntity?)null);

        var req = MockRequest(new AcceptInvitationRequest(ValidToken, "user-1", "dev-1", Array.Empty<byte>()));
        var result = await functions.AcceptInvitation(req, ValidToken, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AcceptInvitation_AlreadyAccepted_Returns409Conflict()
    {
        var (functions, invitationStore, groupStore) = Build();
        invitationStore.GetInvitationByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(InvitationEntity("Accepted"));
        groupStore.GetGroupAsync(GroupId, Arg.Any<CancellationToken>()).Returns(GroupEntity());

        var req = MockRequest(new AcceptInvitationRequest(ValidToken, "user-1", "dev-1", Array.Empty<byte>()));
        var result = await functions.AcceptInvitation(req, ValidToken, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task AcceptInvitation_Expired_Returns410Gone()
    {
        var (functions, invitationStore, _) = Build();
        invitationStore.GetInvitationByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(InvitationEntity("Pending", DateTimeOffset.UtcNow.AddMinutes(-5)));

        var req = MockRequest(new AcceptInvitationRequest(ValidToken, "user-1", "dev-1", Array.Empty<byte>()));
        var result = await functions.AcceptInvitation(req, ValidToken, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(410, obj.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitation_Valid_Returns200WithGroupIdAndContainer()
    {
        var (functions, invitationStore, groupStore) = Build();
        invitationStore.GetInvitationByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(InvitationEntity("Pending"));
        groupStore.GetGroupAsync(GroupId, Arg.Any<CancellationToken>()).Returns(GroupEntity());

        var req = MockRequest(new AcceptInvitationRequest(ValidToken, "user-1", "dev-1", Array.Empty<byte>()));
        var result = await functions.AcceptInvitation(req, ValidToken, CancellationToken.None);

        var obj = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AcceptInvitationResponse>(obj.Value);
        Assert.Equal(GroupId, response.GroupId);
        Assert.Equal("container-1", response.ContainerName);
    }

    [Fact]
    public async Task AcceptInvitation_Valid_MarksInvitationAccepted()
    {
        var (functions, invitationStore, groupStore) = Build();
        invitationStore.GetInvitationByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(InvitationEntity("Pending"));
        groupStore.GetGroupAsync(GroupId, Arg.Any<CancellationToken>()).Returns(GroupEntity());

        var req = MockRequest(new AcceptInvitationRequest(ValidToken, "user-1", "dev-1", Array.Empty<byte>()));
        await functions.AcceptInvitation(req, ValidToken, CancellationToken.None);

        await invitationStore.Received(1).UpdateStatusAsync(GroupId, InvitationId, "Accepted", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeclineInvitation_Valid_Returns204()
    {
        var (functions, invitationStore, _) = Build();
        invitationStore.GetInvitationByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(InvitationEntity("Pending"));

        var req = Substitute.For<HttpRequest>();
        var result = await functions.DeclineInvitation(req, ValidToken, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeclineInvitation_TokenNotFound_Returns404()
    {
        var (functions, invitationStore, _) = Build();
        invitationStore.GetInvitationByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TableEntity?)null);

        var req = Substitute.For<HttpRequest>();
        var result = await functions.DeclineInvitation(req, ValidToken, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
