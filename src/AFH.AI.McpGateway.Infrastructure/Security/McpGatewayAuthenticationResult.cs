using AFH.Common.AI.Actor;

namespace AFH.AI.McpGateway.Infrastructure.Security;

/// <summary>
/// Describes the outcome of authenticating an MCP caller.
/// </summary>
public sealed record McpGatewayAuthenticationResult(
    bool Succeeded,
    AiActorContext? Actor,
    string ErrorCode,
    string ErrorMessage)
{
    /// <summary>
    /// Creates a successful authentication result.
    /// </summary>
    public static McpGatewayAuthenticationResult Success(AiActorContext actor) =>
        new(true, actor, string.Empty, string.Empty);

    /// <summary>
    /// Creates a failed authentication result.
    /// </summary>
    public static McpGatewayAuthenticationResult Failure(string code, string message) =>
        new(false, null, code, message);
}
