using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Functions;
using LuSplit.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LuSplit.Functions.Tests;

public sealed class KeyFunctionsTests
{
    private const string GroupId = "group-1";
    private const string DeviceId = "device-1";

    private static (KeyFunctions functions, IKeyStore keyStore) Build()
    {
        var keyStore = Substitute.For<IKeyStore>();
        var logger = Substitute.For<ILogger<KeyFunctions>>();
        keyStore.EnsureTableExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return (new KeyFunctions(keyStore, logger), keyStore);
    }

    private static HttpRequest UploadRequest(UploadRotatedKeyRequest body)
    {
        var context = new DefaultHttpContext();
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(body);
        context.Request.Body = new MemoryStream(json);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = json.Length;
        return context.Request;
    }

    private static HttpRequest GetRequest(string? deviceId = null)
    {
        var context = new DefaultHttpContext();
        if (deviceId is not null)
            context.Request.QueryString = new QueryString($"?deviceId={deviceId}");
        return context.Request;
    }

    [Fact]
    public async Task UploadRotatedKey_ValidRequest_Returns200()
    {
        var (sut, keyStore) = Build();
        keyStore.GetCurrentKeyVersionAsync(GroupId, Arg.Any<CancellationToken>()).Returns(1);

        var request = new UploadRotatedKeyRequest(2, [new WrappedKeyEntryDto(DeviceId, [0x01])]);
        var result = await sut.UploadRotatedKey(UploadRequest(request), GroupId, CancellationToken.None);

        Assert.IsType<OkResult>(result);
        await keyStore.Received(1).SaveWrappedKeyAsync(GroupId, 2, DeviceId, Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadRotatedKey_VersionNotMonotonic_Returns409()
    {
        var (sut, keyStore) = Build();
        keyStore.GetCurrentKeyVersionAsync(GroupId, Arg.Any<CancellationToken>()).Returns(5);

        var request = new UploadRotatedKeyRequest(3, [new WrappedKeyEntryDto(DeviceId, [0x01])]);
        var result = await sut.UploadRotatedKey(UploadRequest(request), GroupId, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
    }

    [Fact]
    public async Task UploadRotatedKey_ValidatesVersionMonotonic_AcceptsEqualPlusOne()
    {
        var (sut, keyStore) = Build();
        keyStore.GetCurrentKeyVersionAsync(GroupId, Arg.Any<CancellationToken>()).Returns(0);

        var request = new UploadRotatedKeyRequest(1, [new WrappedKeyEntryDto(DeviceId, [0x01])]);
        var result = await sut.UploadRotatedKey(UploadRequest(request), GroupId, CancellationToken.None);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task GetWrappedKeysForDevice_MissingDeviceId_Returns400()
    {
        var (sut, _) = Build();

        var result = await sut.GetWrappedKeysForDevice(GetRequest(), GroupId, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetWrappedKeysForDevice_ReturnsCorrectVersions()
    {
        var (sut, keyStore) = Build();
        keyStore.GetAllWrappedKeysForDeviceAsync(GroupId, DeviceId, Arg.Any<CancellationToken>())
            .Returns(new List<(string, int, byte[])>
            {
                (DeviceId, 1, [0x11]),
                (DeviceId, 2, [0x22])
            } as IReadOnlyList<(string, int, byte[])>);

        var result = await sut.GetWrappedKeysForDevice(GetRequest(DeviceId), GroupId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GetWrappedKeysForDeviceResponse>(ok.Value);
        Assert.Equal(2, response.KeyVersions.Count);
        Assert.Equal(1, response.KeyVersions[0].KeyVersion);
        Assert.Equal(2, response.KeyVersions[1].KeyVersion);
    }
}
