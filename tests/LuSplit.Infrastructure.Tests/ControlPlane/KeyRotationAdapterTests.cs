using System.Net;
using System.Text.Json;
using LuSplit.Application.KeyManagement.Ports;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Infrastructure.ControlPlane;

namespace LuSplit.Infrastructure.Tests.ControlPlane;

public sealed class KeyRotationAdapterTests
{
    private static (KeyRotationAdapter adapter, List<HttpRequestMessage> requests) BuildAdapter(
        HttpStatusCode statusCode,
        object? responseBody = null)
    {
        var captured = new List<HttpRequestMessage>();
        var handler = new KeyCaptureHandler(statusCode, responseBody, captured);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://control-plane.test/") };
        var cpClient = new ControlPlaneHttpClient(httpClient, _ => Task.FromResult<string?>(null));
        return (new KeyRotationAdapter(cpClient), captured);
    }

    [Fact]
    public async Task UploadRotatedKeyAsync_PostsToCorrectEndpoint()
    {
        var (adapter, requests) = BuildAdapter(HttpStatusCode.OK);

        var request = new UploadRotatedKeyRequest(2,
            [new WrappedKeyEntryDto("dev-1", [0x01, 0x02, 0x03])]);

        await adapter.UploadRotatedKeyAsync("group-1", request, CancellationToken.None);

        Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.Contains("api/groups/group-1/keys", requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetWrappedKeysForDeviceAsync_ReturnsKeyChain()
    {
        var expected = new GetWrappedKeysForDeviceResponse(
            [new GroupKeyVersionDto(1, [0x11, 0x22]),
             new GroupKeyVersionDto(2, [0x33, 0x44])]);

        var (adapter, requests) = BuildAdapter(HttpStatusCode.OK, expected);

        var result = await adapter.GetWrappedKeysForDeviceAsync("group-1", "dev-1", CancellationToken.None);

        Assert.Equal(2, result.KeyVersions.Count);
        Assert.Equal(1, result.KeyVersions[0].KeyVersion);
        Assert.Equal(2, result.KeyVersions[1].KeyVersion);
        Assert.Single(requests);
        Assert.Equal(HttpMethod.Get, requests[0].Method);
        Assert.Contains("deviceId=dev-1", requests[0].RequestUri!.Query);
    }

    private sealed class KeyCaptureHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object? _body;
        private readonly List<HttpRequestMessage> _captured;

        public KeyCaptureHandler(HttpStatusCode statusCode, object? body, List<HttpRequestMessage> captured)
        {
            _statusCode = statusCode;
            _body = body;
            _captured = captured;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _captured.Add(request);
            var content = _body is null ? null : JsonSerializer.Serialize(_body);
            var response = new HttpResponseMessage(_statusCode);
            if (content is not null)
                response.Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
    }
}
