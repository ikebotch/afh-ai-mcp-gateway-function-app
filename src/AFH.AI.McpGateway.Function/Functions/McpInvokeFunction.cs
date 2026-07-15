using System.Net;
using AFH.AI.McpGateway.Application.Services;
using AFH.AI.McpGateway.Function.Http;
using AFH.AI.McpGateway.Infrastructure.Security;
using AFH.Common.Mcp.Protocol;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.AI.McpGateway.Function.Functions;

/// <summary>
/// Exposes MCP Gateway tool invocation endpoints.
/// </summary>
public sealed class McpInvokeFunction(
    ToolInvocationService invocationService,
    McpGatewayAuthenticator authenticator)
{
    /// <summary>
    /// Invokes a named MCP tool.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="toolName">The tool name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The HTTP response.</returns>
    [Function(nameof(InvokeTool))]
    public async Task<HttpResponseData> InvokeTool(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mcp/v1/tools/{toolName}/invoke")] HttpRequestData request,
        string toolName,
        CancellationToken cancellationToken)
    {
        var auth = await authenticator.AuthenticateAsync(
            CreateAuthenticationRequest(request),
            cancellationToken).ConfigureAwait(false);
        if (!auth.Succeeded || auth.Actor is null)
        {
            return await request.WriteJsonAsync(
                HttpStatusCode.Unauthorized,
                new McpErrorResponse(auth.ErrorCode, auth.ErrorMessage, GetCorrelationId(request))).ConfigureAwait(false);
        }

        var body = await request.ReadFromJsonAsync<McpToolInvocationRequest>(cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            return await request.WriteJsonAsync(
                HttpStatusCode.BadRequest,
                new McpErrorResponse("invalid_request", "A JSON invocation body is required.", GetCorrelationId(request))).ConfigureAwait(false);
        }

        try
        {
            var response = await invocationService.InvokeAsync(toolName, body, auth.Actor, cancellationToken).ConfigureAwait(false);
            return await request.WriteJsonAsync((HttpStatusCode)response.StatusCode, response).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            return await request.WriteJsonAsync(
                HttpStatusCode.Forbidden,
                new McpErrorResponse("forbidden", ex.Message, GetCorrelationId(request))).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return await request.WriteJsonAsync(
                HttpStatusCode.NotFound,
                new McpErrorResponse("tool_not_found", ex.Message, GetCorrelationId(request))).ConfigureAwait(false);
        }
    }

    private static McpGatewayAuthenticationRequest CreateAuthenticationRequest(HttpRequestData request)
    {
        var permissions = HeaderValues(request, "x-afh-ai-permissions")
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();

        return new McpGatewayAuthenticationRequest(
            HeaderValues(request, "Authorization").FirstOrDefault(),
            HeaderValues(request, "x-afh-ai-gateway-key").FirstOrDefault(),
            GetCorrelationId(request),
            HeaderValues(request, "x-afh-ai-actor-id").FirstOrDefault() ?? "unknown-actor",
            HeaderValues(request, "x-afh-ai-agent-id").FirstOrDefault() ?? "unknown-agent",
            HeaderValues(request, "x-afh-tenant-id").FirstOrDefault(),
            permissions,
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
