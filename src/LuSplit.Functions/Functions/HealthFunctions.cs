using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace LuSplit.Functions.Functions;

public sealed class HealthFunctions
{
    [Function("HealthCheck")]
    public HttpResponseData HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req,
        CancellationToken ct = default)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain");
        _ = response.WriteStringAsync("LuSplit Functions OK");
        return response;
    }
}
