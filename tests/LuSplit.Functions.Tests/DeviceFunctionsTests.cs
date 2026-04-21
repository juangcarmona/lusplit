using Azure.Data.Tables;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Functions.Functions;
using LuSplit.Functions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LuSplit.Functions.Tests;

public sealed class DeviceFunctionsTests
{
    private const string UserId = "user-1";
    private const string DeviceId = "dev-1";

    private static (DeviceFunctions functions, IDeviceStore deviceStore) Build()
    {
        var deviceStore = Substitute.For<IDeviceStore>();
        var logger = Substitute.For<ILogger<DeviceFunctions>>();

        deviceStore.EnsureTableExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        deviceStore.SaveDeviceAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        deviceStore.RevokeDeviceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return (new DeviceFunctions(deviceStore, logger), deviceStore);
    }

    private static HttpRequest MockRequest<T>(T body, string? userId = UserId)
    {
        var context = new DefaultHttpContext();
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(body);
        context.Request.Body = new MemoryStream(json);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = json.Length;
        if (userId is not null)
            context.Request.Headers["X-User-Id"] = userId;
        return context.Request;
    }

    private static HttpRequest GetRequest(string? userId = UserId, string? userIdQuery = null)
    {
        var context = new DefaultHttpContext();
        if (userId is not null)
            context.Request.Headers["X-User-Id"] = userId;
        if (userIdQuery is not null)
            context.Request.QueryString = new QueryString($"?userId={userIdQuery}");
        return context.Request;
    }

    private static TableEntity DeviceEntity(string isRevoked = "False") =>
        new(UserId, DeviceId)
        {
            ["DeviceName"] = "My Phone",
            ["Platform"] = "Android",
            ["RegisteredAt"] = DateTimeOffset.UtcNow,
            ["IsRevoked"] = bool.Parse(isRevoked)
        };

    [Fact]
    public async Task RegisterDevice_MissingUserIdHeader_Returns401()
    {
        var (functions, _) = Build();
        var req = MockRequest(new RegisterDeviceRequest(DeviceId, "My Phone", "Android", Array.Empty<byte>()), userId: null);

        var result = await functions.RegisterDevice(req, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, obj.StatusCode);
    }

    [Fact]
    public async Task RegisterDevice_Valid_Returns201()
    {
        var (functions, _) = Build();
        var req = MockRequest(new RegisterDeviceRequest(DeviceId, "My Phone", "Android", Array.Empty<byte>()));

        var result = await functions.RegisterDevice(req, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, obj.StatusCode);
    }

    [Fact]
    public async Task RegisterDevice_Valid_SavesDevice()
    {
        var (functions, deviceStore) = Build();
        var req = MockRequest(new RegisterDeviceRequest(DeviceId, "My Phone", "Android", Array.Empty<byte>()));

        await functions.RegisterDevice(req, CancellationToken.None);

        await deviceStore.Received(1).SaveDeviceAsync(DeviceId, UserId, "My Phone", "Android", Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListDevices_ValidUserIdQuery_Returns200()
    {
        var (functions, deviceStore) = Build();
        deviceStore.ListDevicesAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new List<TableEntity> { DeviceEntity() });

        var req = GetRequest(userId: null, userIdQuery: UserId);
        var result = await functions.ListDevices(req, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ListDevices_ReturnsDeviceDtos()
    {
        var (functions, deviceStore) = Build();
        deviceStore.ListDevicesAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new List<TableEntity> { DeviceEntity() });

        var req = GetRequest();
        var result = await functions.ListDevices(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ListDevicesResponse>(ok.Value);
        Assert.Single(response.Devices);
        Assert.Equal(DeviceId, response.Devices[0].DeviceId);
    }

    [Fact]
    public async Task RevokeDevice_NotFound_Returns404()
    {
        var (functions, deviceStore) = Build();
        deviceStore.GetDeviceAsync(UserId, DeviceId, Arg.Any<CancellationToken>())
            .Returns((TableEntity?)null);

        var req = GetRequest();
        var result = await functions.RevokeDevice(req, DeviceId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RevokeDevice_Valid_Returns204()
    {
        var (functions, deviceStore) = Build();
        deviceStore.GetDeviceAsync(UserId, DeviceId, Arg.Any<CancellationToken>())
            .Returns(DeviceEntity());

        var req = GetRequest();
        var result = await functions.RevokeDevice(req, DeviceId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }
}
