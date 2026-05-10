using System.Net;
using System.Net.Http.Headers;

namespace LuSplit.Infrastructure.ControlPlane;

public sealed class ControlPlaneHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly Func<CancellationToken, Task<string?>> _tokenProvider;

    private static readonly int[] RetryDelaysMs = [500, 1000, 2000];

    public ControlPlaneHttpClient(HttpClient httpClient, Func<CancellationToken, Task<string?>> tokenProvider)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await _tokenProvider(ct);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Force-buffer content so Content-Length is known before sending.
        // JsonContent.TryComputeLength returns false (unknown size), and
        // AndroidMessageHandler may send an empty body when Content-Length is not set.
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync();

        // Log outgoing request (absolute URI after BaseAddress resolution)
        System.Diagnostics.Debug.WriteLine($"[ControlPlane] {request.Method} {request.RequestUri}");

        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt <= RetryDelaysMs.Length; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    // Clone the request for retry since HttpRequestMessage is single-use
                    request = await CloneRequestAsync(request, token);
                    await Task.Delay(RetryDelaysMs[attempt - 1], ct);
                }

                response = await _httpClient.SendAsync(request, ct);

                if (!IsTransient(response.StatusCode))
                {
                    if ((int)response.StatusCode >= 400)
                    {
                        var body = await response.Content.ReadAsStringAsync(ct);
                        System.Diagnostics.Debug.WriteLine($"[ControlPlane] {request.Method} {request.RequestUri} → {response.StatusCode}: {body}");
                    }
                    return response;
                }

                if (attempt == RetryDelaysMs.Length)
                    return response; // Exhausted retries
            }
            catch (HttpRequestException) when (attempt < RetryDelaysMs.Length)
            {
                await Task.Delay(RetryDelaysMs[attempt], ct);
            }
        }

        return response!;
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status == HttpStatusCode.RequestTimeout ||
        status == HttpStatusCode.TooManyRequests ||
        (int)status >= 500;

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original, string? token)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        if (original.Content is not null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            if (original.Content.Headers.ContentType is not null)
                clone.Content.Headers.ContentType = original.Content.Headers.ContentType;
        }
        if (token is not null)
            clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return clone;
    }
}
