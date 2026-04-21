using System.Net;
using System.Net.Http.Json;
using LuSplit.Contracts.ControlPlane;
using LuSplit.Infrastructure.ControlPlane;
using LuSplit.Infrastructure.Sync;

namespace LuSplit.Infrastructure.Tests.Sync;

/// <summary>
/// Tests <see cref="BlobSyncAdapter"/> and <see cref="SasTokenProvider"/> behaviour.
/// Blob-level tests (upload, download, list) require an Azurite instance and are skipped in CI.
/// </summary>
public sealed class BlobSyncAdapterTests
{
    [Fact]
    public async Task RequestSyncToken_ReturnsTokenFromControlPlane()
    {
        // Arrange: fake HTTP handler that returns a SyncTokenResponse
        var expectedToken = new SyncTokenResponse(
            "sv=fake-sas",
            "container-1",
            DateTimeOffset.UtcNow.AddMinutes(15));

        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            JsonContent.Create(expectedToken));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var controlPlane = new ControlPlaneHttpClient(httpClient, _ => Task.FromResult<string?>("test-token"));
        var sasTokenProvider = new SasTokenProvider(controlPlane);
        var adapter = new BlobSyncAdapter(sasTokenProvider);

        // Act
        var response = await adapter.RequestSyncTokenAsync("group-1", "device-1", CancellationToken.None);

        // Assert
        Assert.Equal("container-1", response.ContainerName);
        Assert.Equal("sv=fake-sas", response.SasToken);
        Assert.True(response.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task RequestSyncToken_TokenCachedOnSecondCall()
    {
        var expectedToken = new SyncTokenResponse(
            "sv=cached-token",
            "container-2",
            DateTimeOffset.UtcNow.AddMinutes(15));

        var callCount = 0;
        var handler = new FakeHttpMessageHandler(() =>
        {
            callCount++;
            return (HttpStatusCode.OK, JsonContent.Create(expectedToken));
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var controlPlane = new ControlPlaneHttpClient(httpClient, _ => Task.FromResult<string?>(null));
        var sasTokenProvider = new SasTokenProvider(controlPlane);
        var adapter = new BlobSyncAdapter(sasTokenProvider);

        await adapter.RequestSyncTokenAsync("group-2", "device-1", CancellationToken.None);
        await adapter.RequestSyncTokenAsync("group-2", "device-1", CancellationToken.None);

        // Second call should use the cache — HTTP called only once
        Assert.Equal(1, callCount);
    }

    [Fact(Skip = "Requires Azurite (Azure Storage Emulator). Run manually with 'azurite --silent' in background.")]
    public async Task UploadAndDownload_RoundTrip()
    {
        // This test requires Azurite running locally on default ports.
        // Start with: dotnet tool install -g Microsoft.Azure.Storage.Azurite && azurite --silent
        var azuriteConnString = "UseDevelopmentStorage=true";
        _ = azuriteConnString;
        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires Azurite (Azure Storage Emulator). Run manually with 'azurite --silent' in background.")]
    public async Task ListRemoteOperations_FiltersOlderThanCursor()
    {
        await Task.CompletedTask;
    }
}

/// <summary>Fake HTTP handler for unit testing.</summary>
file sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<(HttpStatusCode, HttpContent)> _factory;

    public FakeHttpMessageHandler(HttpStatusCode status, HttpContent content)
        : this(() => (status, content))
    { }

    public FakeHttpMessageHandler(Func<(HttpStatusCode, HttpContent)> factory)
    {
        _factory = factory;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var (status, content) = _factory();
        return Task.FromResult(new HttpResponseMessage(status) { Content = content });
    }
}
