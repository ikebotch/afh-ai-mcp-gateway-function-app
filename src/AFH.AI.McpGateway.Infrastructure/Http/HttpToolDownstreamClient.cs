using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    IOptions<McpGatewayOptions> options,
    ISnowflakeAgentAuthenticator snowflakeAgentAuthenticator) : IToolDownstreamClient
{
    private static readonly Regex RouteParameterPattern = new(@"\{(?<name>[^}]+)\}", RegexOptions.Compiled);
    private const string SnowflakeAgentToolName = "snowflake.ask_agent";
    private const string SnowflakeUserAgent = "AFH-AI-MCP-Gateway/1.0";

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

        if (IsSnowflakeAgentTool(tool))
        {
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            message.Headers.UserAgent.ParseAdd(SnowflakeUserAgent);
            snowflakeAgentAuthenticator.Apply(message);
        }
        else if (!string.IsNullOrWhiteSpace(actor.DelegatedAuthorizationHeader))
        {
            message.Headers.TryAddWithoutValidation("Authorization", actor.DelegatedAuthorizationHeader);
        }

        if (!string.IsNullOrWhiteSpace(options.Value.DownstreamApiKey))
        {
            message.Headers.TryAddWithoutValidation("x-afh-internal-api-key", options.Value.DownstreamApiKey);
        }

        if (IsSnowflakeAgentTool(tool))
        {
            message.Content = JsonContent.Create(CreateSnowflakeAgentPayload(arguments));
        }
        else if (tool.Endpoint.Method is "POST" or "PUT" or "PATCH")
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
        if (string.IsNullOrWhiteSpace(route))
        {
            return baseUrl.TrimEnd('/');
        }

        var pathParameterNames = RouteParameterPattern
            .Matches(route)
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in tool.Parameters)
        {
            if (arguments.ValueKind == JsonValueKind.Object &&
                arguments.TryGetProperty(parameter.Name, out var value) &&
                value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            {
                route = route.Replace("{" + parameter.Name + "}", Uri.EscapeDataString(value.ToString()), StringComparison.Ordinal);
            }
        }

        var target = new Uri(CreateServiceBaseUri(baseUrl), route.TrimStart('/'));
        if (!string.Equals(tool.Endpoint.Method, "GET", StringComparison.OrdinalIgnoreCase) ||
            arguments.ValueKind != JsonValueKind.Object)
        {
            return target.ToString();
        }

        var query = BuildQueryString(arguments, pathParameterNames);
        if (string.IsNullOrWhiteSpace(query))
        {
            return target.ToString();
        }

        var separator = string.IsNullOrEmpty(target.Query) ? "?" : "&";
        return target + separator + query;
    }

    private static Uri CreateServiceBaseUri(string baseUrl)
    {
        var uri = new Uri(baseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/')))
        {
            return uri;
        }

        return new Uri(uri, "api/");
    }

    private static string BuildQueryString(JsonElement arguments, ISet<string> excludedParameterNames)
    {
        var parts = new List<string>();
        foreach (var property in arguments.EnumerateObject())
        {
            if (excludedParameterNames.Contains(property.Name) ||
                property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined or JsonValueKind.Object)
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    AddQueryValue(parts, property.Name, item);
                }

                continue;
            }

            AddQueryValue(parts, property.Name, property.Value);
        }

        return string.Join("&", parts);
    }

    private static void AddQueryValue(ICollection<string> parts, string name, JsonElement value)
    {
        var stringValue = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return;
        }

        parts.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(stringValue)}");
    }

    private static bool IsSnowflakeAgentTool(AiToolDescriptor tool)
        => string.Equals(tool.Name, SnowflakeAgentToolName, StringComparison.OrdinalIgnoreCase);

    private static object CreateSnowflakeAgentPayload(JsonElement arguments)
    {
        var question = arguments.ValueKind == JsonValueKind.Object &&
                       arguments.TryGetProperty("question", out var questionElement) &&
                       questionElement.ValueKind == JsonValueKind.String
            ? questionElement.GetString()
            : null;

        return new
        {
            stream = false,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = question ?? string.Empty
                        }
                    }
                }
            }
        };
    }
}
