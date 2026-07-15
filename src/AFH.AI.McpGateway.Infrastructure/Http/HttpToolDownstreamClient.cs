using System.Net.Http.Json;
using System.Text.Json;
using AFH.AI.McpGateway.Application.Abstractions;
using AFH.AI.McpGateway.Application.Models;
using AFH.AI.McpGateway.Infrastructure.Options;
using AFH.Common.AI.Actor;
using AFH.Common.AI.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AFH.AI.McpGateway.Infrastructure.Http;

/// <summary>
/// Invokes service-owned HTTP APIs for registered AI tools.
/// </summary>
public sealed class HttpToolDownstreamClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IOptions<McpGatewayOptions> options) : IToolDownstreamClient
{
    /// <inheritdoc />
    public async Task<ToolDownstreamResult> InvokeAsync(
        AiToolDescriptor tool,
        JsonElement arguments,
        AiActorContext actor,
        CancellationToken cancellationToken)
    {
        var target = BuildTarget(tool, arguments);

        if (ShouldDryRun(tool))
        {
            return new ToolDownstreamResult(
                202,
                JsonSerializer.SerializeToElement(new
                {
                    dryRun = true,
                    tool = tool.Name,
                    owner = tool.OwnerService,
                    target,
                    actor = actor.ActorId,
                    agent = actor.AgentId,
                    actor.CorrelationId
                }),
                target,
                true);
        }

        using var message = new HttpRequestMessage(new HttpMethod(tool.Endpoint.Method), target);
        message.Headers.TryAddWithoutValidation("x-correlation-id", actor.CorrelationId);
        message.Headers.TryAddWithoutValidation("x-afh-ai-actor-id", actor.ActorId);
        message.Headers.TryAddWithoutValidation("x-afh-ai-agent-id", actor.AgentId);
        message.Headers.TryAddWithoutValidation("x-afh-ai-approved", actor.IsApproved.ToString());

        if (!string.IsNullOrWhiteSpace(actor.IdempotencyKey))
        {
            message.Headers.TryAddWithoutValidation("x-idempotency-key", actor.IdempotencyKey);
        }

        if (!string.IsNullOrWhiteSpace(actor.ApprovalId))
        {
            message.Headers.TryAddWithoutValidation("x-afh-ai-approval-id", actor.ApprovalId);
        }

        if (!string.IsNullOrWhiteSpace(actor.DelegatedAuthorizationHeader))
        {
            message.Headers.TryAddWithoutValidation("Authorization", actor.DelegatedAuthorizationHeader);
        }

        if (!string.IsNullOrWhiteSpace(options.Value.DownstreamApiKey))
        {
            message.Headers.TryAddWithoutValidation("x-afh-internal-api-key", options.Value.DownstreamApiKey);
        }

        if (tool.Endpoint.Method is "POST" or "PUT" or "PATCH")
        {
            message.Content = JsonContent.Create(arguments);
        }

        var client = httpClientFactory.CreateClient("afh-downstream-tools");
        using var response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var content = string.IsNullOrWhiteSpace(body)
            ? JsonSerializer.SerializeToElement(new { })
            : JsonSerializer.Deserialize<JsonElement>(body);

        return new ToolDownstreamResult((int)response.StatusCode, content, target, false);
    }

    private bool ShouldDryRun(AiToolDescriptor tool)
    {
        if (!options.Value.DryRunDownstreamCalls)
        {
            return false;
        }

        return !options.Value.RealDownstreamTools.Contains(tool.Name, StringComparer.OrdinalIgnoreCase);
    }

    private string BuildTarget(AiToolDescriptor tool, JsonElement arguments)
    {
        var baseUrl = configuration[tool.Endpoint.ServiceBaseUrlSetting];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = $"https://unconfigured.local/{tool.OwnerService.ToLowerInvariant().Replace(" ", "-")}";
        }

        var route = tool.Endpoint.RouteTemplate;
        foreach (var parameter in tool.Parameters)
        {
            if (arguments.ValueKind == JsonValueKind.Object &&
                arguments.TryGetProperty(parameter.Name, out var value) &&
                value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            {
                route = route.Replace("{" + parameter.Name + "}", Uri.EscapeDataString(value.ToString()), StringComparison.Ordinal);
            }
        }

        return new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), route.TrimStart('/')).ToString();
    }
}
