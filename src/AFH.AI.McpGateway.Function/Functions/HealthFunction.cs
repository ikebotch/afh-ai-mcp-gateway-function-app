using System.Net;
using AFH.AI.McpGateway.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.AI.McpGateway.Function.Functions;

/// <summary>
/// Exposes a lightweight health endpoint.
/// </summary>
public sealed class HealthFunction
{
    /// <summary>
    /// Returns the MCP Gateway health status.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The HTTP response.</returns>
    [Function(nameof(GetHealth))]
    public Task<HttpResponseData> GetHealth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData request)
    {
        return request.WriteJsonAsync(HttpStatusCode.OK, new { status = "Healthy", service = "AFH AI MCP Gateway" });
    }
}
