using System.Text.Json;
using AFH.AI.McpGateway.Application.Models;
using AFH.Common.AI.Actor;
using AFH.Common.AI.Tools;

namespace AFH.AI.McpGateway.Application.Abstractions;

/// <summary>
/// Invokes downstream service endpoints for registered tools.
/// </summary>
public interface IToolDownstreamClient
{
    /// <summary>
    /// Invokes the downstream owner service for a tool.
    /// </summary>
    /// <param name="tool">The tool descriptor.</param>
    /// <param name="arguments">The AI-supplied arguments.</param>
    /// <param name="actor">The AI actor context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The downstream invocation result.</returns>
    Task<ToolDownstreamResult> InvokeAsync(
        AiToolDescriptor tool,
        JsonElement arguments,
        AiActorContext actor,
        CancellationToken cancellationToken);
}
