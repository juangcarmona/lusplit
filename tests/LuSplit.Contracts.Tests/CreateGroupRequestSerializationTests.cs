using System.Net.Http.Json;
using System.Text.Json;
using LuSplit.Contracts.ControlPlane;

namespace LuSplit.Contracts.Tests;

public sealed class ControlPlaneSerializationTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    // ── JsonContent.Create produces camelCase in .NET 10 ────────────────────

    [Fact]
    public async Task JsonContent_Create_Produces_CamelCase()
    {
        var request = BuildCreateGroupRequest();
        var content = JsonContent.Create(request);
        var json = await content.ReadAsStringAsync();

        Assert.Contains("\"groupId\"", json);
        Assert.Contains("\"wrappedKeys\"", json);
        Assert.DoesNotContain("\"GroupId\"", json);
    }

    // ── Null options (used by SyncFunctions, InvitationFunctions, etc.) ─────

    [Fact]
    public void NullOptions_LosesCamelCaseFields_OnDeserialization()
    {
        // This is the bug: null options = case-sensitive, camelCase JSON silently
        // deserializes to null/default for PascalCase constructor params.
        var camelCaseJson = """{"groupId":"g1","ownerId":"u1","ownerDeviceId":"d1","initialKeyVersion":1,"wrappedKeys":[]}""";

        var result = JsonSerializer.Deserialize<CreateGroupRequest>(camelCaseJson);

        // Without case-insensitive, the record gets null for all string fields
        Assert.Null(result!.GroupId);
        Assert.Null(result.OwnerId);
        Assert.Null(result.OwnerDeviceId);
    }

    [Fact]
    public void NullOptions_SyncTokenRequest_LosesCamelCaseFields()
    {
        var camelCaseJson = """{"groupId":"g1","deviceId":"d1"}""";

        var result = JsonSerializer.Deserialize<SyncTokenRequest>(camelCaseJson);

        Assert.Null(result!.GroupId);
        Assert.Null(result.DeviceId);
    }

    // ── Web defaults fix all endpoints ──────────────────────────────────────

    [Fact]
    public void WebDefaults_DeserializesCamelCase_Correctly()
    {
        var camelCaseJson = """{"groupId":"g1","ownerId":"u1","ownerDeviceId":"d1","initialKeyVersion":1,"wrappedKeys":[{"deviceId":"d1","wrappedKey":"AQID"}]}""";

        var result = JsonSerializer.Deserialize<CreateGroupRequest>(camelCaseJson, WebOptions);

        Assert.Equal("g1", result!.GroupId);
        Assert.Equal("u1", result.OwnerId);
        Assert.Equal("d1", result.OwnerDeviceId);
        Assert.Equal(1, result.InitialKeyVersion);
        Assert.Single(result.WrappedKeys);
        Assert.Equal("d1", result.WrappedKeys[0].DeviceId);
    }

    [Fact]
    public void WebDefaults_DeserializesPascalCase_Correctly()
    {
        var pascalJson = """{"GroupId":"g1","OwnerId":"u1","OwnerDeviceId":"d1","InitialKeyVersion":1,"WrappedKeys":[{"DeviceId":"d1","WrappedKey":"AQID"}]}""";

        var result = JsonSerializer.Deserialize<CreateGroupRequest>(pascalJson, WebOptions);

        Assert.Equal("g1", result!.GroupId);
        Assert.Equal("u1", result.OwnerId);
    }

    [Fact]
    public async Task FullRoundTrip_JsonContent_To_WebDefaults()
    {
        var original = BuildCreateGroupRequest();
        var content = JsonContent.Create(original);
        var json = await content.ReadAsStringAsync();

        var deserialized = JsonSerializer.Deserialize<CreateGroupRequest>(json, WebOptions);

        Assert.Equal(original.GroupId, deserialized!.GroupId);
        Assert.Equal(original.OwnerId, deserialized.OwnerId);
        Assert.Equal(original.OwnerDeviceId, deserialized.OwnerDeviceId);
        Assert.Equal(original.InitialKeyVersion, deserialized.InitialKeyVersion);
        Assert.Single(deserialized.WrappedKeys);
        Assert.Equal(original.WrappedKeys[0].DeviceId, deserialized.WrappedKeys[0].DeviceId);
        Assert.Equal(original.WrappedKeys[0].WrappedKey, deserialized.WrappedKeys[0].WrappedKey);
    }

    [Fact]
    public async Task FullRoundTrip_AllContracts()
    {
        // Verify all control-plane request types round-trip through JsonContent → Web defaults
        await AssertRoundTrips(new SyncTokenRequest("g1", "d1"));
        await AssertRoundTrips(new CreateInvitationRequest("g1", "u1", "d1"));
        await AssertRoundTrips(new AcceptInvitationRequest("token", "u1", "d1", new byte[] { 1, 2, 3 }));
        await AssertRoundTrips(new RevokeMemberRequest("u1", "caller"));
        await AssertRoundTrips(new TransferOwnershipRequest("new-owner", "caller"));
        await AssertRoundTrips(new RegisterDeviceRequest("d1", "My Phone", "Android", new byte[] { 1 }));
        await AssertRoundTrips(new UploadRotatedKeyRequest(2, [new WrappedKeyEntryDto("d1", new byte[] { 1, 2 })]));
    }

    // ── Response serialization uses Web defaults (camelCase) ────────────────

    [Fact]
    public void WebDefaults_SerializesResponse_InCamelCase()
    {
        var response = new CreateGroupResponse("g1", "grp-g1");
        var json = JsonSerializer.Serialize(response, WebOptions);

        Assert.Contains("\"groupId\"", json);
        Assert.Contains("\"containerName\"", json);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static CreateGroupRequest BuildCreateGroupRequest() =>
        new("g1", "user-1", "device-1", 1,
            [new WrappedKeyEntryDto("device-1", new byte[] { 1, 2, 3 })]);

    private static async Task AssertRoundTrips<T>(T value)
    {
        var content = JsonContent.Create(value);
        var json = await content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<T>(json, WebOptions);
        Assert.NotNull(result);
    }
}
