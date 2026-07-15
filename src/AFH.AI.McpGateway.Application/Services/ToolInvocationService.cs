using System.Diagnostics;
using System.Text.Json;
using AFH.AI.McpGateway.Application.Abstractions;
using AFH.Common.AI.Actor;
using AFH.Common.AI.Audit;
using AFH.Common.AI.Tools;
using AFH.Common.Mcp.Protocol;

namespace AFH.AI.McpGateway.Application.Services;

/// <summary>
/// Coordinates policy checks, downstream invocation, and audit for tool calls.
/// </summary>
public sealed class ToolInvocationService(
    IToolRegistry toolRegistry,
    IToolDownstreamClient downstreamClient,
    IAiAuditSink auditSink)
{
    /// <summary>
    /// Invokes a registered tool.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="request">The MCP invocation request.</param>
    /// <param name="actor">The actor context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The MCP invocation response.</returns>
    public async Task<McpToolInvocationResponse> InvokeAsync(
        string toolName,
        McpToolInvocationRequest request,
        AiActorContext actor,
        CancellationToken cancellationToken)
    {
        if (!toolRegistry.TryGetTool(toolName, out var tool) || tool is null)
        {
            throw new InvalidOperationException($"Tool '{toolName}' is not registered.");
        }

        EnsureAuthorized(tool, actor);

        var stopwatch = Stopwatch.StartNew();
        var result = await downstreamClient.InvokeAsync(tool, request.Arguments, actor, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        await auditSink.WriteAsync(
            new AiAuditEvent(
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow,
                actor.ActorId,
                actor.AgentId,
                tool.Name,
                result.StatusCode is >= 200 and < 400 ? "Succeeded" : "Failed",
                actor.CorrelationId,
                tool.OwnerService,
                result.Target,
                result.IsDryRun ? "DryRun" : "Real",
                result.StatusCode,
                stopwatch.ElapsedMilliseconds,
                result.StatusCode is >= 200 and < 400 ? null : $"Downstream returned HTTP {result.StatusCode}."),
            cancellationToken).ConfigureAwait(false);

        return new McpToolInvocationResponse(tool.Name, actor.CorrelationId, result.StatusCode, result.Content);
    }

    private static void EnsureAuthorized(AiToolDescriptor tool, AiActorContext actor)
    {
        if (tool.SideEffectLevel is AiToolSideEffectLevel.Write or AiToolSideEffectLevel.HighRisk &&
            !actor.Permissions.Contains("ai.write", StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Tool '{tool.Name}' requires write access.");
        }

        var missing = tool.RequiredPermissions
            .Where(required => !actor.Permissions.Contains(required, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (missing.Length > 0)
        {
            throw new UnauthorizedAccessException($"Tool '{tool.Name}' requires permission '{missing[0]}'.");
        }
    }
}
