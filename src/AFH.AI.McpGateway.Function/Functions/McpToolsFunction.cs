using System.Net;
using AFH.AI.McpGateway.Application.Abstractions;
using AFH.AI.McpGateway.Function.Http;
using AFH.AI.McpGateway.Infrastructure.Security;
using AFH.Common.Mcp.Protocol;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.AI.McpGateway.Function.Functions;

/// <summary>
/// Exposes MCP Gateway tool discovery endpoints.
/// </summary>
public sealed class McpToolsFunction(
    IToolRegistry toolRegistry,
    McpGatewayAuthenticator authenticator)
{
    /// <summary>
    /// Returns the registered Phase 1 tool list.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The HTTP response.</returns>
    [Function(nameof(GetTools))]
    public async Task<HttpResponseData> GetTools(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mcp/v1/tools")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var auth = await authenticator.AuthenticateAsync(
            CreateAuthenticationRequest(request),
            cancellationToken).ConfigureAwait(false);
        if (!auth.Succeeded)
        {
            return await request.WriteJsonAsync(
                HttpStatusCode.Unauthorized,
                new McpErrorResponse(auth.ErrorCode, auth.ErrorMessage, GetCorrelationId(request))).ConfigureAwait(false);
        }

        return await request.WriteJsonAsync(HttpStatusCode.OK, new
        {
            version = "2026-07-phase-1",
            tools = toolRegistry.GetTools()
        }).ConfigureAwait(false);
    }

    private static McpGatewayAuthenticationRequest CreateAuthenticationRequest(HttpRequestData request)
    {
        return new McpGatewayAuthenticationRequest(
            HeaderValues(request, "Authorization").FirstOrDefault(),
            HeaderValues(request, "x-afh-ai-gateway-key").FirstOrDefault(),
            GetCorrelationId(request),
            HeaderValues(request, "x-afh-ai-actor-id").FirstOrDefault(),
            HeaderValues(request, "x-afh-ai-agent-id").FirstOrDefault(),
            HeaderValues(request, "x-afh-tenant-id").FirstOrDefault(),
            HeaderValues(request, "x-afh-ai-permissions")
                .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToArray(),
            HeaderValues(request, "x-idempotency-key").FirstOrDefault(),
            HeaderValues(request, "x-afh-ai-approval-id").FirstOrDefault(),
            HeaderValues(request, "x-afh-ai-approved").Any(value =>
                bool.TryParse(value, out var parsed) && parsed));
    }

    private static string GetCorrelationId(HttpRequestData request)
    {
        return HeaderValues(request, "x-correlation-id").FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    }

    private static IEnumerable<string> HeaderValues(HttpRequestData request, string name)
    {
        return request.Headers.TryGetValues(name, out var values) ? values : [];
    }
}
