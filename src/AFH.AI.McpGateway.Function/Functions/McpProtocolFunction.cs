using System.Net;
using System.Text.Json;
using AFH.AI.McpGateway.Application.Abstractions;
using AFH.AI.McpGateway.Application.Services;
using AFH.AI.McpGateway.Function.Http;
using AFH.AI.McpGateway.Infrastructure.Options;
using AFH.AI.McpGateway.Infrastructure.Security;
using AFH.Common.AI.Tools;
using AFH.Common.Mcp.Protocol;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.AI.McpGateway.Function.Functions;

/// <summary>
/// Exposes the JSON-RPC MCP endpoint used by agent clients.
/// </summary>
public sealed class McpProtocolFunction(
    IToolRegistry toolRegistry,
    ToolInvocationService invocationService,
    McpGatewayAuthenticator authenticator,
    IOptions<McpGatewayOptions> options,
    ILogger<McpProtocolFunction> logger)
{
    private const string JsonRpcVersion = "2.0";
    private const string ProtocolVersion = "2025-06-18";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Handles MCP JSON-RPC requests.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The HTTP response.</returns>
    [Function(nameof(HandleMcp))]
    public async Task<HttpResponseData> HandleMcp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mcp")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        McpRequestDiagnostics.LogIfEnabled(
            request,
            options.Value.Authentication,
            logger,
            nameof(HandleMcp));

        McpJsonRpcRequest? rpcRequest;
        try
        {
            rpcRequest = await request.ReadFromJsonAsync<McpJsonRpcRequest>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return await request.WriteJsonAsync(
                HttpStatusCode.BadRequest,
                Error(null, -32700, "Parse error.")).ConfigureAwait(false);
        }

        if (rpcRequest is null || string.IsNullOrWhiteSpace(rpcRequest.Method))
        {
            return await request.WriteJsonAsync(
                HttpStatusCode.BadRequest,
                Error(rpcRequest?.Id, -32600, "Invalid request.")).ConfigureAwait(false);
        }

        if (string.Equals(rpcRequest.Method, McpMethods.NotificationsInitialized, StringComparison.Ordinal))
        {
            return request.CreateResponse(HttpStatusCode.Accepted);
        }

        var auth = await authenticator.AuthenticateAsync(
            CreateAuthenticationRequest(request),
            cancellationToken).ConfigureAwait(false);
        if (!auth.Succeeded || auth.Actor is null)
        {
            return await request.WriteJsonAsync(
                HttpStatusCode.Unauthorized,
                Error(rpcRequest.Id, -32001, auth.ErrorMessage)).ConfigureAwait(false);
        }

        var response = rpcRequest.Method switch
        {
            McpMethods.Initialize => Success(rpcRequest.Id, CreateInitializeResult()),
            McpMethods.ToolsList => Success(rpcRequest.Id, CreateToolsListResult()),
            McpMethods.ToolsCall => await CallToolAsync(rpcRequest, auth.Actor, cancellationToken).ConfigureAwait(false),
            _ => Error(rpcRequest.Id, -32601, $"Method '{rpcRequest.Method}' is not supported.")
        };

        return await request.WriteJsonAsync(HttpStatusCode.OK, response).ConfigureAwait(false);
    }

    private async Task<McpJsonRpcResponse> CallToolAsync(
        McpJsonRpcRequest rpcRequest,
        AFH.Common.AI.Actor.AiActorContext actor,
        CancellationToken cancellationToken)
    {
        if (rpcRequest.Params is null ||
            rpcRequest.Params.Value.ValueKind != JsonValueKind.Object ||
            !rpcRequest.Params.Value.TryGetProperty("name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
        {
            return Error(rpcRequest.Id, -32602, "tools/call requires a string 'name' parameter.");
        }

        var toolName = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return Error(rpcRequest.Id, -32602, "tools/call requires a non-empty tool name.");
        }

        var arguments = rpcRequest.Params.Value.TryGetProperty("arguments", out var argumentsElement)
            ? argumentsElement
            : JsonSerializer.SerializeToElement(new { }, JsonOptions);

        var reason = rpcRequest.Params.Value.TryGetProperty("reason", out var reasonElement) &&
                     reasonElement.ValueKind == JsonValueKind.String
            ? reasonElement.GetString()
            : null;

        try
        {
            var invocation = await invocationService
                .InvokeAsync(toolName, new McpToolInvocationRequest(arguments, reason), actor, cancellationToken)
                .ConfigureAwait(false);

            return Success(rpcRequest.Id, CreateToolCallResult(invocation));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Error(rpcRequest.Id, -32003, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error(rpcRequest.Id, -32602, ex.Message);
        }
    }

    private JsonElement CreateInitializeResult()
    {
        return JsonSerializer.SerializeToElement(new
        {
            protocolVersion = ProtocolVersion,
            capabilities = new
            {
                tools = new
                {
                    listChanged = false
                }
            },
            serverInfo = new
            {
                name = "afh-ai-mcp-gateway",
                version = "2026.7.0"
            }
        }, JsonOptions);
    }

    private JsonElement CreateToolsListResult()
    {
        return JsonSerializer.SerializeToElement(new
        {
            tools = toolRegistry.GetTools().Select(tool => new
            {
                tool.Name,
                tool.Description,
                inputSchema = CreateInputSchema(tool)
            })
        }, JsonOptions);
    }

    private static JsonElement CreateInputSchema(AiToolDescriptor tool)
    {
        var properties = tool.Parameters.ToDictionary(
            parameter => parameter.Name,
            parameter => new
            {
                type = parameter.Type,
                description = parameter.Description
            });

        return JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties,
            required = tool.Parameters
                .Where(parameter => parameter.Required)
                .Select(parameter => parameter.Name)
                .ToArray()
        }, JsonOptions);
    }

    private static JsonElement CreateToolCallResult(McpToolInvocationResponse invocation)
    {
        var responseJson = JsonSerializer.Serialize(invocation.Content, JsonOptions);
        return JsonSerializer.SerializeToElement(new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = responseJson
                }
            },
            structuredContent = invocation.Content,
            isError = invocation.StatusCode is < 200 or >= 400
        }, JsonOptions);
    }

    private static McpJsonRpcResponse Success(JsonElement? id, JsonElement result)
    {
        return new McpJsonRpcResponse(JsonRpcVersion, id, result);
    }

    private static McpJsonRpcResponse Error(JsonElement? id, int code, string message)
    {
        return new McpJsonRpcResponse(JsonRpcVersion, id, null, new McpJsonRpcError(code, message));
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
