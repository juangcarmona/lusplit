using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Infrastructure.ControlPlane;

namespace LuSplit.Infrastructure.Tests.ControlPlane;

public sealed class GroupRegistrationAdapterTests
{
    private static (GroupRegistrationAdapter adapter, List<HttpRequestMessage> requests) BuildAdapter(
        HttpStatusCode statusCode,
        object? responseBody = null)
    {
        var captured = new List<HttpRequestMessage>();
        var handler = new CaptureHandler(statusCode, responseBody, captured);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://control-plane.test/") };
        var cpClient = new ControlPlaneHttpClient(httpClient, _ => Task.FromResult<string?>(null));
        return (new GroupRegistrationAdapter(cpClient), captured);
    }

    [Fact]
    public async Task RegisterGroupAsync_MapsRequestCorrectly()
    {
        var expected = new CreateGroupResponse("group-1", "grp-group1");
        var (adapter, requests) = BuildAdapter(HttpStatusCode.Created, expected);

        var request = new CreateGroupRequest("group-1", "user-1", "device-1", 1,
            [new WrappedKeyEntryDto("device-1", [0x01, 0x02])]);

        var result = await adapter.RegisterGroupAsync(request, CancellationToken.None);

        Assert.Equal("group-1", result.GroupId);
        Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.Contains("api/groups", requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task RegisterGroupAsync_Handles409Conflict()
    {
        var (adapter, _) = BuildAdapter(HttpStatusCode.Conflict);

        var request = new CreateGroupRequest("dup-1", "user-1", "device-1", 1,
            [new WrappedKeyEntryDto("device-1", [0x01])]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.RegisterGroupAsync(request, CancellationToken.None));
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object? _body;
        private readonly List<HttpRequestMessage> _captured;

        public CaptureHandler(HttpStatusCode statusCode, object? body, List<HttpRequestMessage> captured)
        {
            _statusCode = statusCode;
            _body = body;
            _captured = captured;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            _captured.Add(request);
            var response = new HttpResponseMessage(_statusCode);
            if (_body is not null)
                response.Content = JsonContent.Create(_body);
            return Task.FromResult(response);
        }
    }
}
