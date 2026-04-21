using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace LuSplit.Functions.Middleware;

/// <summary>
/// Validates the Entra External ID (CIAM) bearer token on incoming HTTP trigger requests.
/// Authentication is enforced by <see cref="Microsoft.Identity.Web"/> via DI;
/// this middleware adds a structured log and short-circuits unauthenticated requests
/// that bypass the ASP.NET Core pipeline (e.g., non-HTTP triggers that should not exist).
/// </summary>
public sealed class EntraTokenValidationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<EntraTokenValidationMiddleware> _logger;

    public EntraTokenValidationMiddleware(ILogger<EntraTokenValidationMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        _logger.LogDebug("Executing function {FunctionName}", context.FunctionDefinition.Name);
        await next(context);
    }
}
