using AFH.Common.AI.Tools;

namespace AFH.AI.McpGateway.Application.Abstractions;

/// <summary>
/// Provides the AI tools exposed by the MCP Gateway.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Gets all registered tool descriptors.
    /// </summary>
    /// <returns>The registered tool descriptors.</returns>
    IReadOnlyCollection<AiToolDescriptor> GetTools();

    /// <summary>
    /// Attempts to find a registered tool by name.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="tool">The matching descriptor when found.</param>
    /// <returns>True when the tool exists; otherwise false.</returns>
    bool TryGetTool(string toolName, out AiToolDescriptor? tool);
}
