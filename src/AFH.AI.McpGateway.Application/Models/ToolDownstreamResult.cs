using System.Text.Json;

namespace AFH.AI.McpGateway.Application.Models;

/// <summary>
/// Represents the result returned by a downstream service.
/// </summary>
/// <param name="StatusCode">The downstream status code.</param>
/// <param name="Content">The downstream JSON content.</param>
/// <param name="Target">The downstream target URI.</param>
/// <param name="IsDryRun">Whether the downstream call was simulated.</param>
public sealed record ToolDownstreamResult(
    int StatusCode,
    JsonElement Content,
    string Target,
    bool IsDryRun);
